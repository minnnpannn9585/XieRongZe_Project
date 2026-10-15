using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Qingxi.Editor
{
    /// <summary>One-time authoring utility. The saved scenes are the runtime source of truth.</summary>
    public static class QingxiSceneBuilder
    {
        public static readonly string[] SceneNames = { "MainMenu", "RiverTown", "Reservoir", "MountainCreek" };

        [MenuItem("Qingxi/Rebuild Authored Scenes")]
        public static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            QingxiLocalizationBuilder.ImportTables();
            QingxiArtImporter.ImportAll();
            Directory.CreateDirectory("Assets/Scenes");
            var font = Resources.Load<Font>("Fonts/QingxiUI");
            if (font == null) throw new InvalidOperationException("Missing bundled QingxiUI font.");
            var buildScenes = new EditorBuildSettingsScene[SceneNames.Length];
            for (int i = 0; i < SceneNames.Length; i++)
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                int caseIndex = i - 1;
                if (caseIndex < 0) QingxiWorldBuilder.BuildMenu(font);
                else QingxiWorldBuilder.Build(caseIndex, font);

                var light = new GameObject("Main Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var controller = new GameObject("GameFlow").AddComponent<QingxiGame>();
                controller.EditorBuildUi(caseIndex);
                string path = "Assets/Scenes/" + SceneNames[i] + ".unity";
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new IOException("Could not save " + path);
                buildScenes[i] = new EditorBuildSettingsScene(path, true);
                Debug.Log("QINGXI AUTHORING: saved " + path);
            }
            EditorBuildSettings.scenes = buildScenes;
            QingxiWaterBuilder.ClearTextures();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(buildScenes[0].path, OpenSceneMode.Single);
            Debug.Log("QINGXI AUTHORING: four editable scenes and illustration sprites ready.");
        }
    }
}
