using System;
using System.Reflection;
using Qingxi;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Qingxi.Editor
{
    /// <summary>Batch smoke checks. Restores the user's progress, including on assertion failure.</summary>
    public static class PolishChecks
    {
        const string Running = "Qingxi.Checks.Running";
        static int step;
        static double next;
        static int errors;
        static readonly string[] Keys = { "Qingxi.UnlockedThrough", "Qingxi.ClearedMask",
            "Qingxi.Investigation.0", "Qingxi.Investigation.1", "Qingxi.Investigation.2" };

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
            try { CheckSessions(); }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); return; }
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
                Require(game != null, "No game bootstrap");
                switch (step++)
                {
                    case 0: Capture("menu"); Call(game, "EnterCase", 0); break;
                    case 1: Call(game, "BeginInvestigation"); break;
                    case 2:
                        Require(Get<TownExplorer>(game, "explorer").InputEnabled, "Exploration input locked");
                        Call(game, "OpenInteract", 0); Call(game, "OnSurface"); Call(game, "OnDeep"); break;
                    case 3:
                        Require(InvestigationSave.TryLoad(0, out var saved) && saved.Points == 2 && saved.Clues.Count == 2, "Autosave missing");
                        Call(game, "CloseInteract"); Call(game, "ToggleJournal");
                        Require(!Get<TownExplorer>(game, "explorer").InputEnabled, "Journal input leak");
                        break;
                    case 4:
                        Capture("journal");
                        Require(Get<Text>(game, "journalBody").text.Contains(QingxiCases.Get(0).Areas[0].DeepClue), "Missing evidence");
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
                    case 10: Call(game, "BeginInvestigation"); break;
                    case 11: Call(game, "OpenDeduction"); break;
                    case 12: Call(game, "SelectSuspect", 0); Call(game, "ConfirmSuspect"); break;
                    case 13:
                        Require(!Get<bool>(game, "resultCorrect"), "Wrong answer passed");
                        Require(PlayerPrefs.GetInt("Qingxi.UnlockedThrough") == 1, "Failure unlocked case");
                        Call(game, "AbandonToMenu"); break;
                    case 14:
                        int enabled = 0;
                        foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (cam.enabled) enabled++;
                        Require(enabled == 1, "Camera handoff left multiple cameras: " + enabled);
                        Require(errors == 0, "Runtime error logged");
                        Debug.Log("POLISH: runtime resume / journal / input locks / correct and incorrect verdicts / unlock / camera handoff passed.");
                        Finish(0); break;
                }
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Running, false);
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!SessionState.GetBool(Keys[i] + ".Exists", false)) PlayerPrefs.DeleteKey(Keys[i]);
                else if (i < 2) PlayerPrefs.SetInt(Keys[i], int.Parse(SessionState.GetString(Keys[i] + ".Backup", "0")));
                else PlayerPrefs.SetString(Keys[i], SessionState.GetString(Keys[i] + ".Backup", ""));
            }
            PlayerPrefs.Save();
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

        static void Capture(string name)
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
            RenderMode mode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            int previousOrder = canvas.sortingOrder;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.sortingOrder = 32760;
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
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
        static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
