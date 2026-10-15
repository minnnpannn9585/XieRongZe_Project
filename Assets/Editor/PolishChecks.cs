using System;
using System.Reflection;
using Qingxi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Qingxi.Editor
{
    /// <summary>Batch smoke checks. Restores the user's progress, including on assertion failure.</summary>
    public static class PolishChecks
    {
        const string Running = "Qingxi.Checks.Running";
        static int step;
        static int languageStep;
        static int journalLanguageStep;
        static double next;
        static double phaseWaitSince;
        static int errors;
        static readonly string[] Keys = { "Qingxi.UnlockedThrough", "Qingxi.ClearedMask",
            "Qingxi.Investigation.0", "Qingxi.Investigation.1", "Qingxi.Investigation.2", QingxiLocalization.PreferenceKey,
            "Qingxi.Cleanup.0", "Qingxi.Cleanup.1", "Qingxi.Cleanup.2" };

        [InitializeOnLoadMethod]
        static void Install()
        {
            if (!SessionState.GetBool(Running, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
            next = EditorApplication.timeSinceStartup + 1.0;
        }

        public static void Run()
        {
            try
            {
                CheckSessions();
                QingxiCleanupChecks.ValidateSaves();
                QingxiLocalizationChecks.ValidateTables();
                QingxiSceneChecks.Validate();
                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            }
            catch (Exception ex) { Debug.LogException(ex); QingxiLocalizationChecks.RestorePlayModeBuilder(); EditorApplication.Exit(1); return; }
            for (int i = 0; i < Keys.Length; i++)
            {
                SessionState.SetBool(Keys[i] + ".Exists", PlayerPrefs.HasKey(Keys[i]));
                SessionState.SetString(Keys[i] + ".Backup", i < 2 ? PlayerPrefs.GetInt(Keys[i]).ToString() : PlayerPrefs.GetString(Keys[i]));
                PlayerPrefs.DeleteKey(Keys[i]);
            }
            PlayerPrefs.Save();
            SessionState.SetBool(Running, true);
            Install();
            EditorApplication.EnterPlaymode();
        }

        static void CheckSessions()
        {
            foreach (CaseDefinition definition in QingxiCases.All)
            {
                var session = new InvestigationSession(definition);
                Require(!session.TryDeep(-1, out _) && !session.TrySurface(99, out _), "Invalid area accepted");
                for (int i = 0; i < definition.Areas.Length; i++)
                {
                    Require(session.TrySurface(i, out _), "Surface failed");
                    Require(!session.TrySurface(i, out _), "Duplicate surface");
                }
                Require(session.Points == 3, "Surface cost points");
                for (int i = 0; i < 3; i++) Require(session.TryDeep(i, out _), "Deep failed");
                Require(!session.TryDeep(3, out _) && !session.TryDeep(0, out _), "Overspent or duplicated");
                Require(session.Points == 0 && session.Clues.Count == 8, "Incorrect ledger");
                Require(InvestigationSave.TryDecode(definition, InvestigationSave.Encode(session), out var copy), "Save failed");
                Require(copy.Points == 0 && copy.Clues.Count == 8 && copy.GetProgress(2).DeepDone, "Save did not round trip");
                Require(!InvestigationSave.TryDecode(definition, "{bad", out _), "Malformed save accepted");
                string duplicate = "{\"version\":1,\"chapter\":\"" + definition.ChapterName + "\",\"actions\":[0,0]}";
                Require(!InvestigationSave.TryDecode(definition, duplicate, out _), "Duplicate save accepted");
                Require(session.IsCorrect(definition.TruthIndex) && !session.IsCorrect(-1), "Wrong verdict");
                session.Bind(definition);
                Require(session.Points == 3 && session.Clues.Count == 0, "Reset failed");
            }
            Debug.Log("POLISH: all three cases / budgets / duplicate actions / save validation passed.");
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.8;
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<QingxiGame>();
                Require(game != null, "No authored scene controller");
                if (!game.GetComponent<QingxiLocaleController>().IsReady)
                {
                    if (phaseWaitSince == 0) phaseWaitSince = EditorApplication.timeSinceStartup;
                    Require(EditorApplication.timeSinceStartup - phaseWaitSince < 30, "Localization did not initialize");
                    return;
                }
                if (step == 4 && journalLanguageStep < 3)
                {
                    switch (journalLanguageStep++)
                    {
                        case 0:
                            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("zh-Hans"); break;
                        case 1:
                            Require(Get<GameObject>(game, "journalOverlay").activeSelf, "Language switch closed the journal");
                            Require(Get<Text>(game, "journalBody").text.Contains(QingxiCases.Get(0).Areas[0].DeepClue), "Journal did not translate to Chinese");
                            Require(Get<InvestigationSession>(game, "session").Points == 2, "Language switch reset points");
                            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en"); break;
                        case 2:
                            Require(Get<Text>(game, "journalBody").text.Contains(QingxiLocalization.Text(QingxiCases.Get(0).Areas[0].DeepClue)), "Journal did not translate back to English");
                            Require(Get<InvestigationSession>(game, "session").Clues.Count == 2, "Language switch reset evidence");
                            Debug.Log("LOCALIZATION CHECK: open journal and existing evidence retranslate without resetting state."); break;
                    }
                    return;
                }
                if (Get<Coroutine>(game, "phaseRoutine") != null)
                {
                    if (phaseWaitSince == 0) phaseWaitSince = EditorApplication.timeSinceStartup;
                    Require(EditorApplication.timeSinceStartup - phaseWaitSince < 20, "Scene transition did not release input");
                    return;
                }
                phaseWaitSince = 0;
                if (step == 2 && !QingxiPlayerAnimationChecks.Advance(game)) return;
                if ((step == 9 || step == 17 || step == 21) &&
                    !QingxiCleanupChecks.Advance(game, step == 9 ? 0 : step == 17 ? 1 : 2)) return;
                if (step == 0 && languageStep < 5)
                {
                    var language = game.GetComponent<QingxiLocaleController>().LanguageDropdown;
                    switch (languageStep++)
                    {
                        case 0:
                            QingxiLocalizationChecks.CheckLocale("zh-Hans", game);
                            Capture("menu-zh"); language.Show(); break;
                        case 1:
                            Capture("language-dropdown");
                            QingxiLocalizationChecks.ChooseEnglishFromOpenDropdown(game); break;
                        case 2:
                            QingxiLocalizationChecks.CheckLocale("en", game);
                            Capture("menu-en"); language.value = 0; break;
                        case 3:
                            QingxiLocalizationChecks.CheckLocale("zh-Hans", game);
                            language.value = 1; break;
                        case 4:
                            QingxiLocalizationChecks.CheckLocale("en", game);
                            QingxiLocalizationChecks.CheckSaveAcrossLocales();
                            Debug.Log("LOCALIZATION CHECK: real dropdown selection / immediate bilingual refresh / saved startup locale / cross-language saves passed."); break;
                    }
                    return;
                }
                switch (step++)
                {
                    case 0:
                        QingxiSceneChecks.CheckRuntime("MainMenu");
                        Capture("menu"); Press(game, "BtnLevel_0"); break;
                    case 1:
                        QingxiSceneChecks.CheckRuntime("RiverTown");
                        Press(game, "BtnStart"); break;
                    case 2:
                        Capture("river");
                        Require(Get<TownExplorer>(game, "explorer").InputEnabled, "Exploration input locked");
                        Call(game, "OpenInteract", 0); Press(game, "BtnSurface"); Press(game, "BtnDeep"); break;
                    case 3:
                        Require(InvestigationSave.TryLoad(0, out var saved) && saved.Points == 2 && saved.Clues.Count == 2, "Autosave missing");
                        Call(game, "CloseInteract"); Call(game, "ToggleJournal");
                        Require(!Get<TownExplorer>(game, "explorer").InputEnabled, "Journal input leak");
                        break;
                    case 4:
                        Capture("journal");
                        Require(Get<Text>(game, "journalBody").text.Contains(QingxiLocalization.Text(QingxiCases.Get(0).Areas[0].DeepClue)), "Missing evidence");
                        Call(game, "CloseJournal"); Call(game, "AbandonToMenu"); break;
                    case 5: Call(game, "EnterCase", 0); break;
                    case 6:
                        Require(Get<InvestigationSession>(game, "session").Clues.Count == 2, "Resume lost clues");
                        Require(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length >= 1, "No camera");
                        Call(game, "OpenDeduction"); break;
                    case 7: Call(game, "ToggleJournal"); break;
                    case 8:
                        Require(Get<GameObject>(game, "journalOverlay").activeSelf, "Deduction notebook unavailable");
                        Call(game, "CloseJournal"); Call(game, "SelectSuspect", 0); Call(game, "ConfirmSuspect"); break;
                    case 9:
                        Capture("result");
                        Require(PlayerPrefs.GetInt("Qingxi.UnlockedThrough") == 1, "Next case not unlocked");
                        Require(!InvestigationSave.TryLoad(0, out _), "Completed save remained");
                        Call(game, "ContinueAfterResult"); break;
                    case 10:
                        QingxiSceneChecks.CheckRuntime("Reservoir");
                        Press(game, "BtnStart"); break;
                    case 11: Capture("reservoir"); Press(game, "BtnSubmitConclusion"); break;
                    case 12: Call(game, "SelectSuspect", 0); Call(game, "ConfirmSuspect"); break;
                    case 13:
                        Require(!Get<bool>(game, "resultCorrect"), "Wrong answer passed");
                        Require(PlayerPrefs.GetInt("Qingxi.UnlockedThrough") == 1, "Failure unlocked case");
                        Press(game, "BtnRestart"); break;
                    case 14:
                        Require(Get<InvestigationSession>(game, "session").Points == 3, "Retry did not reset budget");
                        Call(game, "BeginInvestigation"); break;
                    case 15: Call(game, "OpenDeduction"); break;
                    case 16: Call(game, "SelectSuspect", 1); Call(game, "ConfirmSuspect"); break;
                    case 17:
                        Require(PlayerPrefs.GetInt("Qingxi.UnlockedThrough") == 2, "Third case not unlocked");
                        Call(game, "ContinueAfterResult"); break;
                    case 18:
                        QingxiSceneChecks.CheckRuntime("MountainCreek");
                        Press(game, "BtnStart"); break;
                    case 19:
                        Capture("creek");
                        var explorer = Get<TownExplorer>(game, "explorer");
                        Require(explorer.InputEnabled && explorer.Player.InputEnabled, "Third case movement locked");
                        Call(game, "OpenInteract", 3); Press(game, "BtnSurface"); Press(game, "BtnDeep");
                        Require(Get<InvestigationSession>(game, "session").Clues.Count == 2, "Third case investigation failed");
                        Call(game, "CloseInteract"); Call(game, "OpenDeduction"); break;
                    case 20: Press(game, "BtnSuspect_2"); Press(game, "BtnConfirmSuspect"); break;
                    case 21:
                        Require(PlayerPrefs.GetInt("Qingxi.ClearedMask") == 7, "All three cases not marked cleared");
                        Press(game, "BtnResultMenu"); break;
                    case 22:
                        QingxiSceneChecks.CheckRuntime("MainMenu");
                        QingxiLocalizationChecks.CheckLocale("en", game);
                        int enabled = 0;
                        foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (cam.enabled) enabled++;
                        Require(enabled == 1, "Camera handoff left multiple cameras: " + enabled);
                        Require(errors == 0, "Runtime error logged");
                        Debug.Log("POLISH: all four authored scenes / button bindings / unchanged runtime hierarchy / resume / journal / input locks / correct and incorrect verdicts / retry / three-case completion passed.");
                        Finish(0); break;
                }
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        static void Finish(int code)
        {
            QingxiPlayerAnimationChecks.StopObserving();
            EditorApplication.update -= Tick;
            SessionState.SetBool(Running, false);
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!SessionState.GetBool(Keys[i] + ".Exists", false)) PlayerPrefs.DeleteKey(Keys[i]);
                else if (i < 2) PlayerPrefs.SetInt(Keys[i], int.Parse(SessionState.GetString(Keys[i] + ".Backup", "0")));
                else PlayerPrefs.SetString(Keys[i], SessionState.GetString(Keys[i] + ".Backup", ""));
            }
            PlayerPrefs.Save();
            QingxiLocalizationChecks.RestorePlayModeBuilder();
            EditorApplication.Exit(code);
        }

        static void Log(string message, string stack, LogType type)
        {
            // Unity's editor search index can fail in a copied project. This is not player code.
            if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase"))
            {
                Debug.LogWarning("POLISH: editor SearchDatabase indexing exception excluded from runtime checks; see preceding stack.");
                return;
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        public static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            System.IO.Directory.CreateDirectory("Logs");
            Camera camera = null;
            foreach (Camera candidate in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (candidate.enabled) camera = candidate;
            Require(camera != null, "No rendering camera");
            var game = UnityEngine.Object.FindAnyObjectByType<QingxiGame>();
            Canvas canvas = game.GetComponentInChildren<Canvas>();
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            Vector3 previousCameraPosition = camera.transform.position;
            RenderMode mode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            int previousOrder = canvas.sortingOrder;
            Canvas[] nestedCanvases = canvas.GetComponentsInChildren<Canvas>(true);
            int[] nestedOrders = new int[nestedCanvases.Length];
            for (int i = 0; i < nestedCanvases.Length; i++) nestedOrders[i] = nestedCanvases[i].sortingOrder;
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1920f / 1080f;
                // A batch Game View may have a different aspect from the screenshot target.
                var follow = camera.GetComponent<CameraFollow>();
                if (follow != null)
                {
                    Vector2 limit = follow.MapSize / 2f - new Vector2(camera.orthographicSize * camera.aspect, camera.orthographicSize);
                    Vector3 position = camera.transform.position;
                    position.x = Mathf.Clamp(position.x, -Mathf.Max(0, limit.x), Mathf.Max(0, limit.x));
                    position.y = Mathf.Clamp(position.y, -Mathf.Max(0, limit.y), Mathf.Max(0, limit.y));
                    camera.transform.position = position;
                }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.sortingOrder = 32760;
                // UGUI dropdowns use a child canvas with overrideSorting. Keep their popup above
                // the temporarily raised screenshot canvas, just as in the normal Game View.
                foreach (Canvas nested in nestedCanvases)
                    if (nested != canvas && nested.overrideSorting) nested.sortingOrder = 32761;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes("Logs/polish-" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = previousCamera;
                canvas.sortingOrder = previousOrder;
                for (int i = 0; i < nestedCanvases.Length; i++)
                    if (nestedCanvases[i] != null) nestedCanvases[i].sortingOrder = nestedOrders[i];
                camera.targetTexture = null;
                camera.ResetAspect();
                camera.transform.position = previousCameraPosition;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
        static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        static void Press(QingxiGame game, string name)
        {
            foreach (Button button in game.GetComponentsInChildren<Button>(true))
            {
                if (button.name != name) continue;
                Require(button.isActiveAndEnabled && button.interactable, "Button unavailable: " + name);
                button.onClick.Invoke();
                return;
            }
            throw new Exception("Missing button: " + name);
        }
        static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
