using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Qingxi;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Qingxi.Editor
{
    public static class QingxiPlayerAnimationBuilder
    {
        const string Folder = "Assets/Art/Characters";
        public const string TexturePath = Folder + "/player_walk.png";
        public const string ControllerPath = Folder + "/PlayerWalk.controller";
        public const string WalkPath = Folder + "/PlayerWalk.anim";
        public const string IdlePath = Folder + "/PlayerIdle.anim";
        const string PrefabPath = "Assets/Prefabs/World/Player.prefab";
        public const float FramesPerSecond = 10f;

        [MenuItem("Qingxi/Player/Import Four Frame Walk Animation")]
        public static void ImportAndApply()
        {
            ImportSprites();
            BuildClips();
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigurePlayer(prefab.GetComponent<PlayerMovement>());
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            // Keep existing prefab instance overrides and map layouts, refreshing their saved references.
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in new[] { "RiverTown", "Reservoir", "MountainCreek" })
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                    var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    if (player == null) throw new InvalidOperationException("No player in " + name);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
                CaptureFrames();
                QingxiSceneChecks.Validate();
            }
            finally
            {
                if (previous.Any(s => s.isActive && s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            }
            Debug.Log("WALK AUTHORING: four aligned sprites, native Idle/Walk Animator, prefab and three scenes saved.");
        }

        public static Sprite[] Frames() => AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();

        static void ImportSprites()
        {
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();
            // Source: 2172 x 724, four 543px cells. A shared vertical crop preserves body scale.
            // Per-frame cap centers align the head instead of recentering the moving hands/feet.
            float[] centers = { 301f, 263.5f, 283.5f, 275f };
            for (int i = 0; i < 4; i++)
            {
                string name = "player_walk_" + i;
                rects.Add(new SpriteRect
                {
                    name = name, rect = new Rect(i * 543, 37, 543, 643),
                    alignment = SpriteAlignment.Custom, pivot = new Vector2(centers[i] / 543f, 2f / 643f),
                    spriteID = previous.TryGetValue(name, out var id) ? id : GUID.Generate()
                });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
        }

        static void BuildClips()
        {
            Sprite[] frames = Frames();
            if (frames.Length != 4) throw new InvalidOperationException("Walk cycle requires exactly four sprites.");
            AnimationClip Clip(string path, Sprite[] poses, bool loop)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
                clip.ClearCurves(); clip.frameRate = FramesPerSecond;
                var keys = new List<ObjectReferenceKeyframe>();
                for (int i = 0; i < poses.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = i / FramesPerSecond, value = poses[i] });
                if (loop) keys.Add(new ObjectReferenceKeyframe { time = poses.Length / FramesPerSecond, value = poses[0] });
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                return clip;
            }
            var idle = Clip(IdlePath, new[] { frames[1] }, false);
            var walk = Clip(WalkPath, frames, true);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
                controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
                var parameters = controller.parameters;
                foreach (var parameter in parameters) if (parameter.name == "MoveSpeed") parameter.defaultFloat = 1;
                controller.parameters = parameters;
                var stateMachine = controller.layers[0].stateMachine;
                var idleState = stateMachine.AddState("Idle"); idleState.motion = idle;
                var walkState = stateMachine.AddState("Walk"); walkState.motion = walk;
                walkState.speedParameterActive = true; walkState.speedParameter = "MoveSpeed";
                stateMachine.defaultState = idleState;
                var begin = idleState.AddTransition(walkState); begin.hasExitTime = false; begin.duration = 0;
                begin.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
                var stop = walkState.AddTransition(idleState); stop.hasExitTime = false; stop.duration = 0;
                stop.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        }

        public static void ConfigurePlayer(PlayerMovement movement)
        {
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) == null) { ImportSprites(); BuildClips(); }
            var renderer = movement.GetComponentInChildren<SpriteRenderer>();
            var frames = Frames();
            renderer.sprite = frames[1];
            renderer.transform.localPosition = Vector3.zero;
            float scale = 1.3f / frames[1].bounds.size.y;
            renderer.transform.localScale = new Vector3(scale, scale, 1);
            var animator = renderer.GetComponent<Animator>();
            if (animator == null) animator = renderer.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var driver = movement.GetComponent<PlayerWalkAnimation>();
            if (driver == null) driver = movement.gameObject.AddComponent<PlayerWalkAnimation>();
            driver.Configure(animator, renderer);
            movement.Configure(renderer);
            EditorUtility.SetDirty(movement);
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(renderer);
        }

        static void CaptureFrames()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Scene preview = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            player.transform.position = Vector3.zero;
            player.GetComponentInChildren<Animator>().enabled = false;
            var cameraGo = new GameObject("Walk Preview Camera"); SceneManager.MoveGameObjectToScene(cameraGo, preview);
            var camera = cameraGo.AddComponent<Camera>(); cameraGo.AddComponent<UniversalAdditionalCameraData>();
            camera.scene = preview;
            camera.orthographic = true; camera.orthographicSize = 0.85f;
            camera.transform.position = new Vector3(0, 0.65f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.957f, 0.945f, 0.910f);
            camera.cullingMask = ~0;
            var target = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
                Directory.CreateDirectory("Logs");
                for (int i = 0; i < 4; i++)
                {
                    clip.SampleAnimation(player.GetComponentInChildren<SpriteRenderer>().gameObject, i / FramesPerSecond);
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                    File.WriteAllBytes("Logs/walk-frame-" + i + ".png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
