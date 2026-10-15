using System;
using System.Collections.Generic;
using System.IO;
using Qingxi;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace Qingxi.Editor
{
    public static class QingxiLocalizationBuilder
    {
        const string Folder = "Assets/Localization";
        [Serializable] public sealed class Translation { public string source; public string english; }
        [Serializable] public sealed class Catalog { public Translation[] entries; }

        [MenuItem("Qingxi/Localization/Import Bilingual String Tables")]
        public static void ImportTables()
        {
            Directory.CreateDirectory(Folder + "/Locales");
            Directory.CreateDirectory(Folder + "/Tables");
            AssetDatabase.Refresh();
            string settingsPath = Folder + "/LocalizationSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            settings.GetStartupLocaleSelectors().Clear();
            settings.GetStartupLocaleSelectors().Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = QingxiLocalization.PreferenceKey });
            settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("zh-Hans") });
            var locales = new List<Locale>();
            foreach (string code in QingxiLocalization.LocaleCodes)
            {
                string localePath = Folder + "/Locales/" + code + ".asset";
                var locale = AssetDatabase.LoadAssetAtPath<Locale>(localePath);
                if (locale == null)
                {
                    locale = Locale.CreateLocale(code);
                    locale.LocaleName = code == "en" ? "English" : "简体中文";
                    AssetDatabase.CreateAsset(locale, localePath);
                }
                LocalizationEditorSettings.AddLocale(locale);
                locales.Add(locale);
            }
            LocalizationSettings.ProjectLocale = locales[0];
            var collection = LocalizationEditorSettings.GetStringTableCollection(QingxiLocalization.TableName);
            if (collection == null)
                collection = LocalizationEditorSettings.CreateStringTableCollection(QingxiLocalization.TableName, Folder + "/Tables", locales);
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Folder + "/Translations.json"));
            var seen = new HashSet<string>();
            foreach (var row in catalog.entries)
            {
                if (string.IsNullOrWhiteSpace(row.source) || string.IsNullOrWhiteSpace(row.english) || !seen.Add(row.source))
                    throw new InvalidDataException("Missing or duplicate translation: " + row.source);
                foreach (var locale in locales)
                {
                    var table = (StringTable)collection.GetTable(locale.Identifier);
                    table.AddEntry(row.source, locale.Identifier.Code == "en" ? row.english : row.source);
                }
            }
            foreach (var table in collection.StringTables)
            {
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(settings);
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(addressables);
            AssetDatabase.SaveAssets();
            Debug.Log("QINGXI LOCALIZATION: imported " + seen.Count + " Chinese/English entries into Unity String Tables.");
        }

        [MenuItem("Qingxi/Localization/Configure Authored Scenes")]
        public static void ConfigureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Configure();
        }

        public static void Configure()
        {
            ImportTables();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            foreach (string sceneName in QingxiSceneBuilder.SceneNames)
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                var game = UnityEngine.Object.FindAnyObjectByType<QingxiGame>();
                if (game == null) throw new InvalidDataException("No QingxiGame in " + sceneName);
                game.EditorConfigureLocalization();
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            Debug.Log("QINGXI LOCALIZATION: four authored scenes configured.");
        }
    }
}
