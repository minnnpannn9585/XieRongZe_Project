using System;
using System.IO;
using Qingxi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Qingxi.Editor
{
    public static class QingxiCleanupBuilder
    {
        [MenuItem("Qingxi/Update Shape Matching Puzzles (First and Third)")]
        public static void UpdatePuzzlesFromMenu()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            UpdatePuzzles();
        }
        public static void UpdatePuzzles() => Install(true);
        public static void UpdatePuzzlesAndBuild()
        {
            UpdatePuzzles();
            QingxiLocalizationChecks.BuildPlayer();
        }
        public static void BuildAndCheck()
        {
            QingxiLocalizationChecks.BuildPlayer();
            QingxiLocalizationChecks.RunUsingBuiltContent();
        }
        public static void InstallAndBuild()
        {
            Install();
            QingxiLocalizationChecks.BuildPlayer();
        }
        [MenuItem("Qingxi/Install or Update Treatment Minigames")]
        public static void FromMenu()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Install();
        }
        public static void Install() => Install(false);
        static void Install(bool puzzlesOnly)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string path in Directory.GetFiles("Assets/Art/Cleanup","*_simple.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.maxTextureSize=2048; importer.wrapMode=TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            QingxiLocalizationBuilder.ImportTables();
            var shader=Shader.Find("Qingxi/Flat Cleanup UI");
            if(shader==null) throw new InvalidOperationException("Missing flat cleanup UI shader");
            const string materialPath="Assets/Art/Cleanup/FlatCleanupUI.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(materialPath)==null)
                AssetDatabase.CreateAsset(new Material(shader),materialPath);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            for(int i=0;i<QingxiCases.Count;i++)
            {
                if(puzzlesOnly && i==1) continue;
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+QingxiGame.SceneNameForCase(i)+".unity");
                var game=UnityEngine.Object.FindAnyObjectByType<QingxiGame>();
                if(game==null) throw new InvalidOperationException("Missing scene controller");
                game.EditorInstallCleanup();
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            if(setup.Length>0 && !string.IsNullOrEmpty(setup[0].path)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            Debug.Log(puzzlesOnly ? "CLEANUP AUTHORING: first and third shape puzzles updated; reservoir scene preserved." :
                "CLEANUP AUTHORING: three editable treatment boards installed with persistent artwork and component bindings.");
        }
    }
}
