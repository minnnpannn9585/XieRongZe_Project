using System;
using L = Qingxi.QingxiLocalization;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>
    /// Coordinates a saved scene's investigation state and authored UI components.
    /// </summary>
    public sealed partial class QingxiGame : MonoBehaviour
    {
        enum Phase
        {
            Menu,
            Intro,
            Investigate,
            Deduce,
            Result,
            Cleanup
        }

        [Serializable]
        sealed class ChoiceView
        {
            public int Index;
            public Image Image;
            public Text Label;
        }

        [Serializable]
        sealed class LevelButton
        {
            public int Index;
            public Button Button;
            public Image Image;
            public Text Label;
        }

        static readonly Color Page = new Color(0.847f, 0.886f, 0.855f, 1f);
        static readonly Color Board = new Color(0.957f, 0.945f, 0.910f, 1f);
        static readonly Color Panel = new Color(0.984f, 0.973f, 0.949f, 1f);
        static readonly Color Card = new Color(0.902f, 0.929f, 0.906f, 1f);
        static readonly Color Paper = new Color(0.110f, 0.157f, 0.141f, 1f);
        static readonly Color Muted = new Color(0.369f, 0.420f, 0.392f, 1f);
        static readonly Color Gold = new Color(0.122f, 0.416f, 0.345f, 1f);
        static readonly Color Ink = new Color(0.973f, 0.961f, 0.925f, 1f);
        static readonly Color SurfaceColor = new Color(0.184f, 0.435f, 0.369f, 1f);
        static readonly Color DeepColor = new Color(0.612f, 0.290f, 0.220f, 1f);
        static readonly Color Ok = new Color(0.122f, 0.416f, 0.345f, 1f);
        static readonly Color Bad = new Color(0.612f, 0.290f, 0.220f, 1f);
        const string UnlockedKey = "Qingxi.UnlockedThrough";
        const string ClearedKey = "Qingxi.ClearedMask";

        public const string MenuSceneName = "MainMenu";
        static readonly string[] CaseSceneNames = { "RiverTown", "Reservoir", "MountainCreek" };

        [SerializeField, Tooltip("-1 = 主菜单，0 = 河镇，1 = 水库，2 = 山溪")]
        int sceneCaseIndex = -1;

        Font uiFont;
        InvestigationSession session;
        int caseIndex;
        int unlockedThrough;
        int clearedMask;
        bool resultCorrect;
        bool loadingScene;
        int? selectedArea;
        int? selectedSuspect;

        [SerializeField] GameObject menuPanel;
        [SerializeField] LevelButton[] levelButtons;
        [SerializeField] GameObject introPanel;
        [SerializeField] GameObject investigatePanel;
        [SerializeField] GameObject deducePanel;
        [SerializeField] GameObject resultPanel;

        [SerializeField] Text introChapter;
        [SerializeField] Text introBody;
        [SerializeField] Text headerTitle;
        [SerializeField] Text mapCaption;
        [SerializeField] Text pointsText;
        [SerializeField] Text promptText;

        [SerializeField] TownExplorer explorer;
        [SerializeField] GameObject interactDim;
        [SerializeField] Text detailTitle;
        [SerializeField] Button surfaceButton;
        [SerializeField] Button deepButton;
        [SerializeField] Text surfaceLabel;
        [SerializeField] Text deepLabel;
        [SerializeField] Text surfaceBody;
        [SerializeField] Text deepBody;
        [SerializeField] Text notesHeader;
        [SerializeField] ScrollRect notebookScroll;
        [SerializeField] Text notebookText;

        [SerializeField] ChoiceView[] choices;
        [SerializeField] Button confirmButton;

        [SerializeField] Image resultStrip;
        [SerializeField] Image resultDim;
        [SerializeField] Text resultTitle;
        [SerializeField] Text resultComment;
        [SerializeField] Text resultChoice;
        [SerializeField] Text resultTruth;
        [SerializeField] Text resultHint;
        [SerializeField] Text restartLabel;

        [SerializeField] CanvasGroup detailGroup;
        [SerializeField] CanvasGroup notebookGroup;
        Coroutine phaseRoutine;
        bool phaseTransitioning;
        Coroutine detailRoutine;
        int shownPoints = -1;
        int shownClues = -1;

        void Awake()
        {
            uiFont = LoadUiFont();
            WarmFont();
            foreach (Text text in GetComponentsInChildren<Text>(true))
                text.font = uiFont;
            LoadProgress();
            PreparePanels();
            if (sceneCaseIndex < 0)
            {
                RefreshMenu();
                Show(Phase.Menu, false);
                return;
            }

            // A level can be played directly in the editor, independently of menu unlocks.
            caseIndex = Mathf.Clamp(sceneCaseIndex, 0, QingxiCases.Count - 1);
            bool resumed = InvestigationSave.TryLoad(caseIndex, out InvestigationSession saved);
            session = resumed ? saved : new InvestigationSession(QingxiCases.Get(caseIndex));
            if (explorer == null)
                explorer = FindAnyObjectByType<TownExplorer>(FindObjectsInactive.Include);
            if (explorer != null)
            {
                explorer.Initialize(session, uiFont);
                explorer.OnOpen = OpenInteract;
                explorer.OnClose = CloseInteract;
            }
            ApplyCaseChrome();
            RefreshInvestigation();
            ShowPreview();
            if (ResumeCleanup()) return;
            if (resumed && session.Clues.Count > 0)
            {
                ShowTown();
                Show(Phase.Investigate, false);
            }
            else
                Show(Phase.Intro, false);
        }

        public static string SceneNameForCase(int index) => CaseSceneNames[index];

        public void PerformAction(QingxiUiAction.Action action, int index)
        {
            if (loadingScene || phaseRoutine != null) return;
            switch (action)
            {
                case QingxiUiAction.Action.EnterCase: EnterCase(index); break;
                case QingxiUiAction.Action.BeginInvestigation: BeginInvestigation(); break;
                case QingxiUiAction.Action.ReturnToMenu: AbandonToMenu(); break;
                case QingxiUiAction.Action.OpenDeduction: OpenDeduction(); break;
                case QingxiUiAction.Action.ToggleJournal: ToggleJournal(); break;
                case QingxiUiAction.Action.CloseJournal: CloseJournal(); break;
                case QingxiUiAction.Action.CloseInteract: CloseInteract(); break;
                case QingxiUiAction.Action.ObserveSurface: OnSurface(); break;
                case QingxiUiAction.Action.InvestigateDeep: OnDeep(); break;
                case QingxiUiAction.Action.SelectSuspect: SelectSuspect(index); break;
                case QingxiUiAction.Action.BackToInvestigation: BackToInvestigation(); break;
                case QingxiUiAction.Action.ConfirmSuspect: ConfirmSuspect(); break;
                case QingxiUiAction.Action.ContinueAfterResult: ContinueAfterResult(); break;
                case QingxiUiAction.Action.CleanupPrimary: cleanupGame.Confirm(); break;
                case QingxiUiAction.Action.ResetCleanup: cleanupGame.ResetBoard(); break;
            }
        }

        void BeginInvestigation()
        {
            ShowTown();
            Show(Phase.Investigate);
            RefreshInvestigation();
        }

        void BackToInvestigation()
        {
            ShowTown();
            Show(Phase.Investigate);
            RefreshInvestigation();
        }

        void OpenInteract(int id)
        {
            if (session == null || id < 0 || id >= session.Case.Areas.Length ||
                activePhase != Phase.Investigate || phaseRoutine != null || journalOverlay.activeSelf)
                return;
            selectedArea = id;
            interactDim.SetActive(true);
            if (explorer != null)
                explorer.MovementLocked = true;
            RefreshDetail();
        }

        void CloseInteract()
        {
            selectedArea = null;
            if (interactDim != null)
                interactDim.SetActive(false);
            if (explorer != null)
                explorer.MovementLocked = activePhase != Phase.Investigate || phaseRoutine != null ||
                    (journalOverlay != null && journalOverlay.activeSelf);
        }

        void OnSurface()
        {
            if (!selectedArea.HasValue)
                return;
            if (!session.TrySurface(selectedArea.Value, out _))
                return;
            InvestigationSave.Store(caseIndex, session);
            RefreshInvestigation();
            if (explorer != null)
                explorer.RefreshMarks();
            Punch(surfaceBody.rectTransform);
        }

        void OnDeep()
        {
            if (!selectedArea.HasValue)
                return;
            if (!session.TryDeep(selectedArea.Value, out _))
                return;
            InvestigationSave.Store(caseIndex, session);
            RefreshInvestigation();
            if (explorer != null)
                explorer.RefreshMarks();
            Punch(deepBody.rectTransform);
        }

        void OpenDeduction()
        {
            CloseInteract();
            if (explorer != null)
                explorer.PresentLandmark();
            selectedSuspect = null;
            Show(Phase.Deduce);
            RefreshSuspects();
        }

        void SelectSuspect(int id)
        {
            if (session == null || activePhase != Phase.Deduce || id < 0 || id >= session.Case.Suspects.Length)
                return;
            selectedSuspect = id;
            RefreshSuspects();
        }

        void ConfirmSuspect()
        {
            if (!selectedSuspect.HasValue || activePhase != Phase.Deduce || phaseRoutine != null)
                return;

            resultCorrect = session.IsCorrect(selectedSuspect.Value);
            cleanupComplete = false;
            if (resultCorrect)
            {
                InvestigationSave.Store(caseIndex, session);
                CleanupSave.Begin(caseIndex);
            }
            else InvestigationSave.Clear(caseIndex);
            resultStrip.color = resultCorrect ? Ok : Bad;
            RefreshResultText();
            resultTitle.color = resultCorrect ? Ok : Bad;
            if (explorer != null)
                explorer.PresentLandmark();
            if (resultDim != null)
            {
                resultDim.color = resultCorrect
                    ? new Color(0.12f, 0.32f, 0.24f, 0.18f)
                    : new Color(0.36f, 0.12f, 0.08f, 0.46f);
            }
            Show(Phase.Result);
            Punch(resultTitle.rectTransform);
        }

        /// <summary>Re-renders translated text without changing investigation or UI state.</summary>
        public void RefreshLanguage()
        {
            if (loadingScene) return;
            if (sceneCaseIndex < 0) { RefreshMenu(); return; }
            if (session == null) return;
            ApplyCaseChrome();
            RefreshInvestigation();
            if (explorer != null) explorer.RefreshMarks();
            if (activePhase == Phase.Result && selectedSuspect.HasValue) RefreshResultText();
            if (cleanupGame != null) cleanupGame.Refresh();
            if (journalOverlay != null && journalOverlay.activeSelf) RefreshJournalContents();
        }

        void RefreshResultText()
        {
            resultTitle.text = L.Text(resultCorrect ? cleanupComplete ? "治理完成" : "结论正确" : "再核对一次证据");
            resultComment.text = L.Text(resultCorrect ? cleanupComplete ? "污染源已控制。后续处理将继续，水体恢复需要时间。" :
                "已锁定污染源。接下来请亲手完成治理，完成后解锁下一关。" : session.Case.FailText);
            resultChoice.text = L.Format("你的结论：{0}", L.Text(session.Case.Suspects[selectedSuspect.Value].Label));
            resultTruth.text = L.Format("正确答案：{0}", L.Text(session.Case.Suspects[session.Case.TruthIndex].Label));
            resultHint.text = L.Text(session.Case.ClueHint);
            restartLabel.text = L.Text(!resultCorrect ? "重新调查" : !cleanupComplete ? "开始治理" : caseIndex < QingxiCases.Count - 1 ? "下一关" : "再调查本关");
        }

        void ContinueAfterResult()
        {
            if (resultCorrect && !cleanupComplete) { BeginCleanup(); return; }
            if (resultCorrect && caseIndex < QingxiCases.Count - 1)
                caseIndex++;
            LoadAuthoredScene(SceneNameForCase(caseIndex));
        }

        void EnterCase(int index)
        {
            if (index < 0 || index >= QingxiCases.Count || index > unlockedThrough)
                return;

            LoadAuthoredScene(SceneNameForCase(index));
        }

        void ShowPreview()
        {
            ShowTown();
            if (explorer != null)
                explorer.PresentLandmark();
        }

        void AbandonToMenu()
        {
            CloseInteract();
            LoadAuthoredScene(MenuSceneName);
        }

        void LoadAuthoredScene(string sceneName)
        {
            if (loadingScene) return;
            loadingScene = true;
            StopAllCoroutines();
            phaseTransitioning = false;
            phaseRoutine = null;
            if (explorer != null)
            {
                explorer.InputEnabled = false;
                explorer.MovementLocked = true;
            }
            foreach (CanvasGroup group in GetPhaseGroups())
                group.interactable = false;
            SceneManager.LoadScene(sceneName);
        }

        void ShowTown()
        {
            if (explorer == null) return;
            explorer.ResumeExplore();
            CloseInteract();
        }

        void Update()
        {
            if (loadingScene) return;
            HandleShortcuts();
            if (promptText == null || explorer == null || !explorer.isActiveAndEnabled)
                return;
            promptText.text = explorer.Prompt;
        }

        void LoadProgress()
        {
            unlockedThrough = PlayerPrefs.GetInt(UnlockedKey, 0);
            clearedMask = PlayerPrefs.GetInt(ClearedKey, 0);
            if (unlockedThrough < 0)
                unlockedThrough = 0;
            if (unlockedThrough > QingxiCases.Count - 1)
                unlockedThrough = QingxiCases.Count - 1;
        }

        void MarkCleared(int index)
        {
            clearedMask |= 1 << index;
            int next = index + 1;
            if (next > unlockedThrough)
                unlockedThrough = next;
            if (unlockedThrough > QingxiCases.Count - 1)
                unlockedThrough = QingxiCases.Count - 1;

            PlayerPrefs.SetInt(UnlockedKey, unlockedThrough);
            PlayerPrefs.SetInt(ClearedKey, clearedMask);
            PlayerPrefs.Save();
        }

        void RefreshMenu()
        {
            if (levelButtons == null) return;
            for (int i = 0; i < levelButtons.Length; i++)
            {
                CaseDefinition current = QingxiCases.Get(i);
                bool locked = i > unlockedThrough;
                bool cleared = (clearedMask & (1 << i)) != 0;
                LevelButton level = levelButtons[i];
                level.Button.interactable = !locked;
                level.Image.color = Card;
                if (locked)
                {
                    level.Label.text = L.Format("第 {0} 关 · {1} · 未解锁", i + 1, L.Text(current.ChapterName));
                    level.Label.color = Muted;
                }
                else if (CleanupSave.TryLoad(i, out _))
                {
                    level.Label.text = L.Format("第 {0} 关 · {1} · 继续治理", i + 1, L.Text(current.ChapterName));
                    level.Label.color = Gold;
                }
                else if (cleared)
                {
                    level.Label.text = L.Format("第 {0} 关 · {1} · 已通关", i + 1, L.Text(current.ChapterName));
                    level.Label.color = Gold;
                }
                else
                {
                    level.Label.text = L.Format("第 {0} 关 · {1}", i + 1, L.Text(current.ChapterName));
                    level.Label.color = Paper;
                }
                if (!locked && InvestigationSave.TryLoad(i, out InvestigationSession saved) && saved.Clues.Count > 0)
                {
                    level.Label.text = L.Format("第 {0} 关 · {1} · 继续调查", i + 1, L.Text(current.ChapterName));
                    level.Image.color = new Color(0.80f, 0.88f, 0.81f);
                }
            }
        }

        void ApplyCaseChrome()
        {
            CaseDefinition current = session.Case;
            introChapter.text = L.Format("第 {0} 关 · {1}", caseIndex + 1, L.Text(current.ChapterName));
            introBody.text = L.Text(current.IntroBody);
            headerTitle.text = L.Text(current.PlaceName);
            mapCaption.text = L.Text(current.MapCaption);
            resultTruth.text = L.Format("正确答案：{0}", L.Text(current.Suspects[current.TruthIndex].Label));
            resultHint.text = L.Text(current.ClueHint);

            for (int i = 0; i < choices.Length; i++)
                choices[i].Label.text = L.Text(current.Suspects[i].Label);
        }

        void RefreshInvestigation()
        {
            RefreshPoints();
            RefreshDetail();
            RefreshNotebook();
        }

        void RefreshPoints()
        {
            if (shownPoints >= 0 && session.Points < shownPoints)
                Punch(pointsText.rectTransform);
            shownPoints = session.Points;
            pointsText.text = L.Format("行动点    {0} / {1}", session.Points, QingxiCases.StartingPoints);
            pointsText.color = session.Points == 0 ? DeepColor : Gold;
        }

        void RefreshDetail()
        {
            if (!selectedArea.HasValue)
                return;

            AreaDefinition def = session.Case.Areas[selectedArea.Value];
            AreaProgress progress = session.GetProgress(selectedArea.Value);
            detailTitle.text = L.Text(def.Name);

            surfaceLabel.text = L.Text(progress.SurfaceDone ? "已表面观察" : "表面观察 · 免费");
            surfaceButton.interactable = !progress.SurfaceDone;
            surfaceBody.text = progress.SurfaceDone
                ? L.Text(def.SurfaceClue)
                : L.Text("尚未观察。信息偏模糊，可能夹带干扰。");
            surfaceBody.color = progress.SurfaceDone ? Paper : Muted;

            if (progress.DeepDone)
            {
                deepLabel.text = L.Text("已深入核查");
                deepButton.interactable = false;
            }
            else if (session.Points < def.DeepCost)
            {
                deepLabel.text = L.Format("点数不足 · 需{0}点", def.DeepCost);
                deepButton.interactable = false;
            }
            else
            {
                deepLabel.text = L.Format("深入核查 · {0}点", def.DeepCost);
                deepButton.interactable = true;
            }

            deepBody.text = progress.DeepDone
                ? L.Text(def.DeepClue)
                : L.Text("尚未核查。关键线索可以直接佐证或排除某个污染源。");
            deepBody.color = progress.DeepDone ? Gold : Muted;
            PlayFade(detailGroup);
        }

        void RefreshNotebook()
        {
            bool changed = shownClues != session.Clues.Count;
            notesHeader.text = session.Clues.Count == 0
                ? L.Text("线索笔记")
                : L.Format("线索笔记    {0}", session.Clues.Count);

            if (session.Clues.Count == 0)
            {
                notebookText.color = Muted;
                notebookText.text = L.Text("还没有线索。\n先用免费的表面观察判断方向，再把调查点数花在值得深挖的区域。");
            }
            else
            {
                notebookText.color = Paper;
                var sb = new StringBuilder();
                for (int i = 0; i < session.Clues.Count; i++)
                {
                    ClueRecord clue = session.Clues[i];
                    string name = L.Text(session.Case.Areas[clue.Area].Name);
                    if (clue.IsKey)
                    {
                        sb.Append("<color=#9C4A38>");
                        sb.Append(L.Text("[关键]"));
                        sb.Append("  ");
                        sb.Append(name);
                        sb.Append(L.Text("："));
                        sb.Append(L.Text(clue.Text));
                        sb.Append("</color>\n\n");
                    }
                    else
                    {
                        sb.Append("<color=#1C2824>");
                        sb.Append(L.Text("[普通]"));
                        sb.Append("  ");
                        sb.Append(name);
                        sb.Append(L.Text("："));
                        sb.Append(L.Text(clue.Text));
                        sb.Append("</color>\n\n");
                    }
                }

                notebookText.text = sb.ToString();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(notebookText.rectTransform);
            if (changed)
                notebookScroll.verticalNormalizedPosition = 0f;
            if (shownClues >= 0 && session.Clues.Count != shownClues)
                PlayFade(notebookGroup);
            shownClues = session.Clues.Count;
        }

        void RefreshSuspects()
        {
            for (int i = 0; i < choices.Length; i++)
            {
                bool on = selectedSuspect == choices[i].Index;
                choices[i].Image.color = on ? Gold : Card;
                choices[i].Label.color = on ? Ink : Paper;
                choices[i].Image.GetComponent<UiPress>().SetRest(on ? 1.03f : 1f);
            }

            confirmButton.interactable = selectedSuspect.HasValue;
        }

        void Show(Phase phase, bool animate = true)
        {
            CloseJournal();
            activePhase = phase;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            if (explorer != null)
            {
                explorer.MovementLocked = true;
                explorer.InputEnabled = false;
            }
            if (phaseRoutine != null)
                StopCoroutine(phaseRoutine);
            phaseRoutine = null;
            phaseTransitioning = false;
            // The scene already contains its opening panel. Make it usable immediately,
            // including during slow loading frames; subsequent phase changes still animate.
            if (!animate)
            {
                FinishTransition(phase);
                return;
            }
            phaseTransitioning = true;
            Coroutine started = StartCoroutine(PlayTransition(phase));
            // A long frame can finish the tween before StartCoroutine returns.
            phaseRoutine = phaseTransitioning ? started : null;
        }

        void PreparePanels()
        {
            GameObject[] panels =
            {
                menuPanel,
                introPanel,
                investigatePanel,
                deducePanel,
                resultPanel,
                cleanupGame != null ? cleanupGame.gameObject : null,
            };
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                CanvasGroup group = panels[i].GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
                panels[i].SetActive(false);
            }
        }

        CanvasGroup GroupOf(Phase phase)
        {
            switch (phase)
            {
                case Phase.Menu:
                    return menuPanel.GetComponent<CanvasGroup>();
                case Phase.Intro:
                    return introPanel.GetComponent<CanvasGroup>();
                case Phase.Investigate:
                    return investigatePanel.GetComponent<CanvasGroup>();
                case Phase.Deduce:
                    return deducePanel.GetComponent<CanvasGroup>();
                case Phase.Cleanup:
                    return cleanupGame.GetComponent<CanvasGroup>();
                default:
                    return resultPanel.GetComponent<CanvasGroup>();
            }
        }

        IEnumerator PlayTransition(Phase next)
        {
            CanvasGroup to = GroupOf(next);
            RectTransform toRect = to.GetComponent<RectTransform>();
            to.gameObject.SetActive(true);
            to.alpha = 0f;
            to.blocksRaycasts = false;
            Vector2 offset = new Vector2(0f, -32f);
            toRect.anchoredPosition = offset;

            CanvasGroup[] groups = GetPhaseGroups();
            float[] start = new float[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                start[i] = groups[i].alpha;
                groups[i].interactable = false;
            }

            const float duration = 0.34f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                to.alpha = k;
                toRect.anchoredPosition = Vector2.Lerp(offset, Vector2.zero, k);
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[i] == to)
                        continue;
                    groups[i].alpha = Mathf.Lerp(start[i], 0f, k);
                    groups[i].blocksRaycasts = false;
                }

                // Do not leave a fully visible panel locked until one more frame.
                if (t >= duration) break;
                yield return null;
            }

            FinishTransition(next);
        }

        void FinishTransition(Phase next)
        {
            CanvasGroup to = GroupOf(next);
            CanvasGroup[] groups = GetPhaseGroups();
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == to)
                    continue;
                groups[i].alpha = 0f;
                groups[i].blocksRaycasts = false;
                groups[i].interactable = false;
                groups[i].gameObject.SetActive(false);
                groups[i].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            to.gameObject.SetActive(true);
            to.alpha = 1f;
            to.blocksRaycasts = true;
            to.interactable = true;
            if (explorer != null)
            {
                explorer.MovementLocked = next != Phase.Investigate || selectedArea.HasValue;
                explorer.InputEnabled = next == Phase.Investigate;
            }
            to.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            phaseTransitioning = false;
            phaseRoutine = null;
        }

        CanvasGroup[] GetPhaseGroups()
        {
            if (sceneCaseIndex < 0)
                return new[] { menuPanel.GetComponent<CanvasGroup>() };
            return new[]
            {
                introPanel.GetComponent<CanvasGroup>(),
                investigatePanel.GetComponent<CanvasGroup>(),
                deducePanel.GetComponent<CanvasGroup>(),
                resultPanel.GetComponent<CanvasGroup>(),
                cleanupGame.GetComponent<CanvasGroup>()
            };
        }

        void PlayFade(CanvasGroup group)
        {
            if (!Application.isPlaying || group == null)
                return;
            if (detailRoutine != null && group == detailGroup)
                StopCoroutine(detailRoutine);
            Coroutine routine = StartCoroutine(FadeGroup(group));
            if (group == detailGroup)
                detailRoutine = routine;
        }

        IEnumerator FadeGroup(CanvasGroup group)
        {
            const float duration = 0.22f;
            float t = 0f;
            group.alpha = 0.2f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }

            group.alpha = 1f;
        }

        void Punch(RectTransform target)
        {
            if (!Application.isPlaying || target == null)
                return;
            StartCoroutine(PunchScale(target));
        }

        IEnumerator PunchScale(RectTransform target)
        {
            const float duration = 0.26f;
            float t = 0f;
            while (t < duration)
            {
                if (target == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float s = 1f + 0.07f * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            if (target != null)
                target.localScale = Vector3.one;
        }

        static Font LoadUiFont()
        {
            Font bundled = Resources.Load<Font>("Fonts/QingxiUI");
            if (bundled != null) return bundled;
            string[] names = { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "WenQuanYi Micro Hei", "SimHei", "SimSun" };
            for (int i = 0; i < names.Length; i++)
            {
                Font font = Font.CreateDynamicFontFromOSFont(names[i], 32);
                if (FontMatches(font, names[i]))
                    return font;
            }

            Debug.LogWarning("清溪治理手记：未找到微软雅黑或黑体，中文可能无法显示。");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static bool FontMatches(Font font, string requested)
        {
            if (font == null || font.fontNames == null)
                return false;

            for (int i = 0; i < font.fontNames.Length; i++)
            {
                string name = font.fontNames[i];
                if (string.IsNullOrEmpty(name))
                    continue;
                if (string.Equals(name, requested, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (name.IndexOf(requested, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        void WarmFont()
        {
            var sb = new StringBuilder();
            sb.Append("清溪治理手记开始调查提交结论表面观察深入核查免费点数不足已观察已深查未查线索笔记");
            sb.Append("锁定主要污染源确认结论返回调查重新调查下一关再调查本关通关失败你的结论正确答案线索提示调查点数");
            sb.Append("选择关卡通关后解锁下一关未解锁已通关放弃关卡返回主菜单行动点按离开移动靠近场所调查未调查第关");
            for (int c = 0; c < QingxiCases.Count; c++)
            {
                CaseDefinition current = QingxiCases.Get(c);
                sb.Append(current.ChapterName);
                sb.Append(current.PlaceName);
                sb.Append(current.MapCaption);
                sb.Append(current.IntroBody);
                sb.Append(current.SuccessText);
                sb.Append(current.FailText);
                sb.Append(current.ClueHint);
                for (int i = 0; i < current.Areas.Length; i++)
                {
                    sb.Append(current.Areas[i].Name);
                    sb.Append(current.Areas[i].ShortName);
                    sb.Append(current.Areas[i].SurfaceClue);
                    sb.Append(current.Areas[i].DeepClue);
                }

                for (int i = 0; i < current.Suspects.Length; i++)
                    sb.Append(current.Suspects[i].Label);
            }

            string corpus = sb.ToString();
            int[] sizes = { 16, 18, 20, 22, 24, 26, 28, 30, 32, 40, 48, 56 };
            for (int i = 0; i < sizes.Length; i++)
            {
                uiFont.RequestCharactersInTexture(corpus, sizes[i], FontStyle.Normal);
                uiFont.RequestCharactersInTexture(corpus, sizes[i], FontStyle.Bold);
            }
        }

    }
}
