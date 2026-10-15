using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Qingxi.Editor
{
    public static class QingxiSceneChecks
    {
        [MenuItem("Qingxi/Validate Authored Scenes")]
        public static void Validate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < QingxiSceneBuilder.SceneNames.Length; i++)
                {
                    string name = QingxiSceneBuilder.SceneNames[i];
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
                    var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                    QingxiLocalizationChecks.ValidateScene(UnityEngine.Object.FindAnyObjectByType<QingxiGame>(), i == 0);
                    Require(all.Count(t => t.GetComponent<QingxiGame>() != null) == 1, name + ": controller missing or duplicated");
                    Require(all.Count(t => t.GetComponent<EventSystem>() != null) == 1, name + ": event system missing or duplicated");
                    Require(all.Count(t => t.GetComponent<Camera>() != null && t.GetComponent<Camera>().enabled) == 1, name + ": camera count");
                    foreach (Transform transform in all)
                    {
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script: " + transform.name);
                        foreach (SpriteRenderer renderer in transform.GetComponents<SpriteRenderer>())
                            Require(renderer.sprite != null && AssetDatabase.Contains(renderer.sprite), "Non-persistent artwork: " + transform.name);
                        foreach (Text text in transform.GetComponents<Text>())
                            Require(text.font != null && AssetDatabase.Contains(text.font), "Non-persistent font: " + transform.name);
                        if (transform.GetComponent<Button>() != null)
                            Require(transform.GetComponent<QingxiUiAction>() != null, "Button has no saved action: " + transform.name);
                    }
                    SessionState.SetInt("Qingxi.AuthoredCount." + name, all.Length);
                    if (i > 0)
                    {
                        CheckWorld(UnityEngine.Object.FindAnyObjectByType<TownExplorer>(), i - 1);
                        QingxiCleanupChecks.ValidateScene(UnityEngine.Object.FindAnyObjectByType<QingxiGame>(), i - 1);
                    }
                    Debug.Log("SCENE CHECK: " + name + " has " + all.Length + " saved objects, persistent sprites and wired UI.");
                }
            }
            finally
            {
                QingxiWaterBuilder.ClearTextures();
                if (previous.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            }
        }

        public static void CheckRuntime(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            Require(scene.name == name, "Expected " + name + ", loaded " + scene.name);
            int saved = SessionState.GetInt("Qingxi.AuthoredCount." + name, -1);
            int current = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
            Require(saved > 0 && current == saved, name + ": hierarchy generated or destroyed at runtime, saved " + saved + ", running " + current);
            Require(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, name + ": multiple cameras");
        }

        static void CheckWorld(TownExplorer world, int caseIndex)
        {
            Require(world != null && world.CaseIndex == caseIndex, "Wrong world binding");
            Require(world.Player != null && world.WorldCamera != null, "Missing player/camera");
            QingxiPlayerAnimationChecks.Validate(world.Player);
            Require(world.Sites.Length == 5 && world.Sites.All(s => s != null), "Missing investigation site");
            Require(world.Sites.Select(s => s.Index).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, 5)), "Duplicated area indices");
            Require(PrefabUtility.IsPartOfPrefabInstance(world.Player), "Player should be an editable prefab instance");
            foreach (var site in world.Sites) Require(PrefabUtility.IsPartOfPrefabInstance(site), "Site should be a prefab instance");
            Physics2D.SyncTransforms();
            CheckReachability(world);
            QingxiWaterChecks.Validate(world);
        }

        static void CheckReachability(TownExplorer world)
        {
            bool[] reached = ReachableSites(world);
            for (int i = 0; i < reached.Length; i++) Require(reached[i], "Unreachable investigation entrance: " + world.Sites[i].name);
        }

        public static bool[] ReachableSites(TownExplorer world) => ReachablePoints(world, world.Sites.Select(s => s.Entrance).ToArray());

        public static bool IsFree(TownExplorer world, Vector2 point) =>
            !Physics2D.OverlapCircleAll(point, 0.34f).Any(c => !c.isTrigger && c.gameObject != world.Player.gameObject);

        public static bool[] ReachablePoints(TownExplorer world, Vector2[] targets)
        {
            const float step = 0.2f;
            Vector2 start = world.Player.transform.position;
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(Vector2Int.zero);
            visited.Add(Vector2Int.zero);
            bool[] reached = new bool[targets.Length];
            Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            bool Free(Vector2 point)
            {
                return IsFree(world, point);
            }
            Require(Free(start), "Player spawn intersects an obstacle");
            while (queue.Count > 0)
            {
                Vector2Int node = queue.Dequeue();
                Vector2 point = start + (Vector2)node * step;
                for (int i = 0; i < targets.Length; i++)
                    if (Vector2.Distance(point, targets[i]) <= 0.24f && Free(targets[i])) reached[i] = true;
                foreach (Vector2Int offset in offsets)
                {
                    Vector2Int next = node + offset;
                    Vector2 nextPoint = start + (Vector2)next * step;
                    if (Mathf.Abs(nextPoint.x) > 11.3f || Mathf.Abs(nextPoint.y) > 8.3f || visited.Contains(next)) continue;
                    visited.Add(next);
                    bool blocked = Physics2D.CircleCastAll(point, 0.34f, offset, step)
                        .Any(hit => hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != world.Player.gameObject);
                    if (Free(nextPoint) && !blocked) queue.Enqueue(next);
                }
            }
            return reached;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
