using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Qingxi;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Qingxi.Editor
{
    public static class QingxiLocalizationChecks
    {
        public static void RunUsingBuiltContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            int builder = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            Require(builder >= 0, "Missing Addressables built-content play mode");
            SessionState.SetInt("Qingxi.Checks.OriginalPlayModeBuilder", settings.ActivePlayModeDataBuilderIndex);
            SessionState.SetBool("Qingxi.Checks.RestorePlayModeBuilder", true);
            settings.ActivePlayModeDataBuilderIndex = builder;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            PolishChecks.Run();
        }

        public static void RestorePlayModeBuilder()
        {
            if (!SessionState.GetBool("Qingxi.Checks.RestorePlayModeBuilder", false)) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt("Qingxi.Checks.OriginalPlayModeBuilder", 0);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            SessionState.SetBool("Qingxi.Checks.RestorePlayModeBuilder", false);
        }

        public static void BuildPlayer()
        {
            QingxiLocalizationBuilder.ImportTables();
            ValidateTables();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/LocalizationSmoke/Qingxi.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Require(report.summary.result == BuildResult.Succeeded, "Localized player build failed: " + report.summary.result);
            Debug.Log("LOCALIZATION CHECK: Windows player and local Addressables content built successfully.");
        }

        public static void ValidateTables()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(QingxiLocalization.TableName);
            Require(collection != null, "Missing official String Table collection");
            var english = (StringTable)collection.GetTable("en");
            var chinese = (StringTable)collection.GetTable("zh-Hans");
            Require(english != null && chinese != null, "Missing bilingual String Tables");
            var catalog = JsonUtility.FromJson<QingxiLocalizationBuilder.Catalog>(File.ReadAllText("Assets/Localization/Translations.json"));
            foreach (var row in catalog.entries)
            {
                Require(chinese.GetEntry(row.source)?.LocalizedValue == row.source, "Changed Chinese source: " + row.source);
                Require(english.GetEntry(row.source)?.LocalizedValue == row.english, "Missing English translation: " + row.source);
                string Placeholders(string text) => string.Join(",", Regex.Matches(text, @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v));
                Require(Placeholders(row.source) == Placeholders(row.english), "Mismatched translation placeholders: " + row.source);
            }
            void Check(string source) => Require(!string.IsNullOrWhiteSpace(english.GetEntry(source)?.LocalizedValue), "Missing case translation: " + source);
            foreach (var definition in QingxiCases.All)
            {
                Check(definition.ChapterName); Check(definition.PlaceName); Check(definition.MapCaption);
                Check(definition.IntroBody); Check(definition.SuccessText); Check(definition.FailText); Check(definition.ClueHint);
                foreach (var area in definition.Areas) { Check(area.Name); Check(area.ShortName); Check(area.SurfaceClue); Check(area.DeepClue); }
                foreach (var suspect in definition.Suspects) Check(suspect.Label);
            }
            Debug.Log("LOCALIZATION CHECK: all " + catalog.entries.Length + " bilingual entries / all three cases / format placeholders passed.");
        }

        public static void ValidateScene(QingxiGame game, bool isMenu)
        {
            Require(game.GetComponent<QingxiLocaleController>() != null, "Missing locale controller");
            var dropdowns = game.GetComponentsInChildren<Dropdown>(true);
            Require(dropdowns.Length == (isMenu ? 1 : 0), "Wrong language dropdown count");
            var table = (StringTable)LocalizationEditorSettings.GetStringTableCollection(QingxiLocalization.TableName).GetTable("en");
            foreach (var label in game.GetComponentsInChildren<QingxiLocalizedText>(true))
                Require(table.GetEntry(label.StringReference.TableEntryReference.Key) != null, "Untranslated static UI: " + label.name);
            if (isMenu)
                Require(dropdowns[0].options.Select(o => o.text).SequenceEqual(new[] { "简体中文", "English" }), "Wrong language choices");
        }

        public static void CheckLocale(string code, QingxiGame game)
        {
            Require(LocalizationSettings.SelectedLocale.Identifier.Code == code, "Wrong selected locale");
            Require(PlayerPrefs.GetString(QingxiLocalization.PreferenceKey) == code, "Language preference not saved");
            var controller = game.GetComponent<QingxiLocaleController>();
            Require(controller.LanguageDropdown.value == (code == "en" ? 1 : 0), "Dropdown out of sync");
            Require(new PlayerPrefLocaleSelector { PlayerPreferenceKey = QingxiLocalization.PreferenceKey }
                .GetStartupLocale(LocalizationSettings.AvailableLocales).Identifier.Code == code, "Saved locale does not restore at startup");
            string expected = code == "en" ? "Qingxi Field Notes" : "清溪治理手记";
            Require(game.GetComponentsInChildren<Text>().Any(t => t.text == expected), "Static menu did not update");
            if (code == "en")
            {
                var catalog = JsonUtility.FromJson<QingxiLocalizationBuilder.Catalog>(File.ReadAllText("Assets/Localization/Translations.json"));
                foreach (var row in catalog.entries)
                    Require(QingxiLocalization.Text(row.source) == row.english, "Runtime translation failed: " + row.source);
            }
        }

        public static void CheckSaveAcrossLocales()
        {
            var session = new InvestigationSession(QingxiCases.Get(0));
            session.TrySurface(0, out _); session.TryDeep(0, out _);
            string before = InvestigationSave.Encode(session);
            foreach (string code in QingxiLocalization.LocaleCodes)
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
                Require(InvestigationSave.Encode(session) == before, "Language changed the save format");
                Require(InvestigationSave.TryDecode(QingxiCases.Get(0), before, out var restored) &&
                    restored.Points == 2 && restored.Clues.Count == 2, "Save incompatible across languages");
            }
        }

        public static void ChooseEnglishFromOpenDropdown(QingxiGame game)
        {
            var dropdown = game.GetComponent<QingxiLocaleController>().LanguageDropdown;
            var option = dropdown.GetComponentsInChildren<Toggle>().FirstOrDefault(t => t.name.StartsWith("Item 1:"));
            Require(option != null, "Dropdown popup is not usable");
            option.isOn = true;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
