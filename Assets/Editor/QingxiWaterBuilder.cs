using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Qingxi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Qingxi.Editor
{
    /// <summary>Bakes illustration-aligned water polygons and vector wooden bridges. Editor only.</summary>
    public static class QingxiWaterBuilder
    {
        const string AssetFolder = "Assets/Art/Bridges";
        const float CellSize = 0.08f;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        [MenuItem("Qingxi/Water/Update Water and Bridges")]
        public static void UpdateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            UpdateScenes();
        }

        public static void UpdateScenes()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < QingxiCases.Count; i++)
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + QingxiGame.SceneNameForCase(i) + ".unity");
                    Build(UnityEngine.Object.FindAnyObjectByType<TownExplorer>(), true);
                    EditorSceneManager.SaveScene(scene);
                    CaptureOverview(UnityEngine.Object.FindAnyObjectByType<TownExplorer>());
                }
                AssetDatabase.SaveAssets();
                QingxiSceneChecks.Validate();
            }
            finally
            {
                ClearTextures();
                if (previous.Any(s => s.isActive && s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            }
        }

        [MenuItem("Qingxi/Water/Rebake Collision in Current Scene")]
        public static void RebakeCurrentCollision()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Rebake collision outside Play Mode.");
            var world = UnityEngine.Object.FindAnyObjectByType<TownExplorer>();
            if (world == null) throw new InvalidOperationException("Open a Qingxi investigation scene first.");
            try
            {
                var bridges = world.GetComponentsInChildren<WalkableBridge>();
                var sources = world.GetComponentsInChildren<WaterArea>().Select(a => a.Source).Distinct().ToArray();
                foreach (var area in world.GetComponentsInChildren<WaterArea>()) UnityEngine.Object.DestroyImmediate(area.gameObject);
                foreach (var source in sources) BakeWater(source, bridges);
                Physics2D.SyncTransforms();
                if (!QingxiSceneChecks.ReachableSites(world).All(r => r))
                    throw new InvalidOperationException("One or more investigation entrances are unreachable after the edit.");
                QingxiWaterChecks.Validate(world);
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }
            finally { ClearTextures(); }
        }

        static void CaptureOverview(TownExplorer world)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Camera camera = world.WorldCamera;
            Vector3 savedPosition = camera.transform.position;
            float size = camera.orthographicSize, aspect = camera.aspect;
            var target = new RenderTexture(1680, 1260, 24);
            var pixels = new Texture2D(1680, 1260, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var labels = world.GetComponentsInChildren<CanvasGroup>();
            float[] alphas = labels.Select(l => l.alpha).ToArray();
            try
            {
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographicSize = 8.35f;
                camera.aspect = 4f / 3f;
                camera.targetTexture = target;
                foreach (var label in labels) label.alpha = 1;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1680, 1260), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/water-" + world.gameObject.scene.name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < labels.Length; i++) labels[i].alpha = alphas[i];
                camera.targetTexture = null; camera.aspect = aspect; camera.orthographicSize = size;
                camera.transform.position = savedPosition;
                RenderTexture.active = previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        public static void Build(TownExplorer world, bool reportBefore = false)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Directory.CreateDirectory(AssetFolder);
            AssetDatabase.Refresh();
            foreach (WaterArea area in world.GetComponentsInChildren<WaterArea>(true))
                UnityEngine.Object.DestroyImmediate(area.gameObject);
            Transform oldBridges = world.transform.Find("Bridges · 桥梁与栈道");
            if (oldBridges != null) UnityEngine.Object.DestroyImmediate(oldBridges.gameObject);

            var sources = world.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sprite != null &&
                (r.sprite.name.EndsWith("_map") || new[] { "fish_pond", "fish_cages", "creek_sampling", "irrigation_fields" }.Contains(r.sprite.name))).ToArray();
            foreach (var source in sources) BakeWater(source, Array.Empty<WalkableBridge>());
            Physics2D.SyncTransforms();
            if (reportBefore)
            {
                bool[] reached = QingxiSceneChecks.ReachableSites(world);
                Debug.Log("WATER BEFORE BRIDGES: " + world.name + " can reach " + reached.Count(r => r) + "/5 entrances on dry land.");
            }

            Transform bridges = new GameObject("Bridges · 桥梁与栈道").transform;
            bridges.SetParent(world.transform, false);
            if (world.CaseIndex == 0)
                MakeBridge(bridges, "River Crossing · 河镇跨河桥", new Vector2(-2.7f, -1.8f), new Vector2(3f, -1.8f), false);
            else if (world.CaseIndex == 1)
                MakeBridge(bridges, "Fish Cage Boardwalk · 网箱调查栈道", new Vector2(-5f, 2.05f), new Vector2(-2.1f, 2.05f), true);
            else
                MakeBridge(bridges, "Creek Crossing · 山溪采样桥", new Vector2(-1.9f, -0.85f), new Vector2(3.5f, -0.85f), false);
            var walkways = bridges.GetComponentsInChildren<WalkableBridge>();
            if (world.CaseIndex == 0) MoveEntrance(world.Sites.Single(s => s.Index == 0), new Vector2(1.8f, -1.8f));
            else if (world.CaseIndex == 1) MoveEntrance(world.Sites.Single(s => s.Index == 1), new Vector2(-3f, 2.05f));
            else
            {
                MoveEntrance(world.Sites.Single(s => s.Index == 0), new Vector2(0.55f, -0.85f));
                MoveEntrance(world.Sites.Single(s => s.Index == 4), new Vector2(-6.4f, -4.55f));
            }
            foreach (var source in sources)
            {
                var previousWater = source.GetComponentInChildren<WaterArea>();
                if (previousWater != null) UnityEngine.Object.DestroyImmediate(previousWater.gameObject);
                BakeWater(source, walkways);
            }
            Physics2D.SyncTransforms();
            Debug.Log("WATER AUTHORING: " + world.name + " saved illustration water colliders and " + walkways.Length + " bridge/pier.");
        }

        static void MoveEntrance(InvestigationSite site, Vector2 position)
        {
            var serialized = new SerializedObject(site);
            var entrance = (Transform)serialized.FindProperty("entrance").objectReferenceValue;
            entrance.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(entrance);
        }

        static void BakeWater(SpriteRenderer source, WalkableBridge[] bridges)
        {
            Bounds bounds = source.bounds;
            int width = Mathf.CeilToInt(bounds.size.x / CellSize);
            int height = Mathf.CeilToInt(bounds.size.y / CellSize);
            Vector2 origin = bounds.min;
            Vector2 step = new Vector2(bounds.size.x / width, bounds.size.y / height);
            var wet = new bool[width, height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 p = origin + Vector2.Scale(new Vector2(x + 0.5f, y + 0.5f), step);
                if (bridges.Any(b => b.ContainsDeck(p))) continue;
                wet[x, y] = IsPaintedWater(source, p);
            }
            var edges = new Dictionary<Vector2Int, List<Vector2Int>>();
            bool Wet(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && wet[x, y];
            void Edge(int x0, int y0, int x1, int y1)
            {
                var start = new Vector2Int(x0, y0);
                if (!edges.TryGetValue(start, out var ends)) edges[start] = ends = new List<Vector2Int>();
                ends.Add(new Vector2Int(x1, y1));
            }
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!wet[x, y]) continue;
                if (!Wet(x, y - 1)) Edge(x, y, x + 1, y);
                if (!Wet(x + 1, y)) Edge(x + 1, y, x + 1, y + 1);
                if (!Wet(x, y + 1)) Edge(x + 1, y + 1, x, y + 1);
                if (!Wet(x - 1, y)) Edge(x, y + 1, x, y);
            }
            var polygons = new List<Vector2[]>();
            while (edges.Count > 0)
            {
                Vector2Int start = edges.First().Key, at = start, direction = Vector2Int.right;
                var loop = new List<Vector2Int>();
                do
                {
                    loop.Add(at);
                    var ends = edges[at];
                    Vector2Int incoming = direction;
                    int Turn(Vector2Int next)
                    {
                        Vector2Int d = next - at;
                        int cross = incoming.x * d.y - incoming.y * d.x;
                        return cross > 0 ? 3 : incoming.x * d.x + incoming.y * d.y > 0 ? 2 : cross < 0 ? 1 : 0;
                    }
                    Vector2Int to = ends.OrderByDescending(Turn).First();
                    ends.Remove(to);
                    if (ends.Count == 0) edges.Remove(at);
                    direction = to - at;
                    at = to;
                } while (at != start);
                var simplified = new List<Vector2>();
                float area = 0;
                for (int i = 0; i < loop.Count; i++)
                {
                    Vector2Int previous = loop[(i + loop.Count - 1) % loop.Count], p = loop[i], next = loop[(i + 1) % loop.Count];
                    area += p.x * next.y - next.x * p.y;
                    if (p - previous == next - p) continue;
                    Vector2 world = origin + Vector2.Scale((Vector2)p, step);
                    simplified.Add(source.transform.InverseTransformPoint(world));
                }
                // Tiny isolated paint flecks are not bodies of water. The maps have no walkable islands.
                if (area >= 3 && simplified.Count >= 3) polygons.Add(simplified.ToArray());
            }
            if (polygons.Count == 0) return;
            var go = new GameObject("Water Barrier · 水面禁止通行");
            go.transform.SetParent(source.transform, false);
            var collider = go.AddComponent<PolygonCollider2D>();
            collider.sharedMaterial = NavigationMaterial();
            collider.pathCount = polygons.Count;
            for (int i = 0; i < polygons.Count; i++) collider.SetPath(i, polygons[i]);
            go.AddComponent<WaterArea>().Configure(source, source.sprite.name.EndsWith("_map"));
        }

        public static bool IsPaintedWater(SpriteRenderer source, Vector2 world)
        {
            Vector2 local = source.transform.InverseTransformPoint(world);
            Sprite sprite = source.sprite;
            Vector2 uv = new Vector2((local.x - sprite.bounds.min.x) / sprite.bounds.size.x,
                (local.y - sprite.bounds.min.y) / sprite.bounds.size.y);
            if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return false;
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (!textures.TryGetValue(path, out var texture) || texture == null)
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.LoadImage(File.ReadAllBytes(path));
                textures[path] = texture;
            }
            Rect rect = sprite.rect;
            Color c = texture.GetPixel(Mathf.Clamp((int)(rect.x + uv.x * rect.width), 0, texture.width - 1),
                Mathf.Clamp((int)(rect.y + uv.y * rect.height), 0, texture.height - 1));
            return c.a > 0.5f && c.b > 0.4f && c.g > 0.38f && c.g > c.r * 1.20f && c.b > c.r * 1.20f;
        }

        public static void ClearTextures()
        {
            foreach (var texture in textures.Values) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            textures.Clear();
        }

        static PhysicsMaterial2D NavigationMaterial()
        {
            string path = AssetFolder + "/Navigation.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material == null)
            {
                material = new PhysicsMaterial2D("Shore and Bridge Sliding") { friction = 0, bounciness = 0 };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static void MakeBridge(Transform parent, string name, Vector2 from, Vector2 to, bool pier)
        {
            Directory.CreateDirectory(AssetFolder);
            AssetDatabase.Refresh();
            float length = Vector2.Distance(from, to);
            const float width = 1.8f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (from + to) / 2f;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            go.AddComponent<WalkableBridge>().Configure(new Vector2(length, width), pier);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            void Quad(float left, float bottom, float right, float top, Color color)
            {
                int index = vertices.Count;
                vertices.AddRange(new[] { new Vector3(left, bottom), new Vector3(right, bottom), new Vector3(right, top), new Vector3(left, top) });
                triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
                Color linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
                colors.AddRange(new[] { linear, linear, linear, linear });
            }
            Color wood = new Color32(215, 166, 67, 255), light = new Color32(228, 186, 94, 255), dark = new Color32(151, 108, 54, 255);
            Color pine = new Color32(44, 87, 73, 255), cream = new Color32(245, 236, 206, 255);
            float half = length / 2f;
            Quad(-half + 0.1f, -1f, half + 0.14f, 0.7f, new Color(0.12f, 0.24f, 0.2f, 0.22f));
            Quad(-half, -width / 2f, half, width / 2f, dark);
            int planks = Mathf.CeilToInt(length / 0.3f);
            for (int i = 0; i < planks; i++)
            {
                float left = -half + i * length / planks + 0.015f;
                Quad(left, -width / 2f + 0.08f, left + length / planks - 0.03f, width / 2f - 0.04f, i % 3 == 0 ? light : wood);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float y = side * (width / 2f - 0.1f);
                Quad(-half - 0.05f, y - 0.08f, half + 0.05f, y + 0.08f, dark);
                Quad(-half - 0.05f, y + 0.025f, half + 0.05f, y + 0.09f, light);
                int posts = Mathf.Max(2, Mathf.CeilToInt(length / 1.4f));
                for (int i = 0; i <= posts; i++)
                {
                    float x = Mathf.Lerp(-half + 0.08f, half - 0.08f, i / (float)posts);
                    Quad(x - 0.09f, y - 0.13f, x + 0.09f, y + 0.12f, pine);
                    Quad(x - 0.1f, y + 0.02f, x + 0.1f, y + 0.15f, cream);
                }
                var rail = go.AddComponent<BoxCollider2D>();
                rail.sharedMaterial = NavigationMaterial();
                rail.offset = new Vector2(0, y);
                rail.size = new Vector2(length + 0.1f, 0.16f);
            }
            if (pier)
            {
                Quad(half - 0.13f, -width / 2f, half + 0.04f, width / 2f, dark);
                var endRail = go.AddComponent<BoxCollider2D>();
                endRail.sharedMaterial = NavigationMaterial();
                endRail.offset = new Vector2(half - 0.05f, 0);
                endRail.size = new Vector2(0.16f, width);
            }
            string meshPath = AssetFolder + "/" + (pier ? "CagePier" : length > 5.5f ? "RiverBridge" : "CreekBridge") + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, meshPath); }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetColors(colors); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            string materialPath = AssetFolder + "/BridgeFlat.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Qingxi/Flat Geometry")); AssetDatabase.CreateAsset(material, materialPath); }
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -700;
            string prefabFolder = "Assets/Prefabs/World";
            Directory.CreateDirectory(prefabFolder);
            Vector3 position = go.transform.position;
            Quaternion rotation = go.transform.rotation;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabFolder + "/" + Path.GetFileNameWithoutExtension(meshPath) + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
