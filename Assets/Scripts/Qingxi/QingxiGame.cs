using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>
    /// Runtime UGUI prototype. Press Play in any scene; the interface is built from color blocks and text.
    /// </summary>
    public sealed class QingxiGame : MonoBehaviour
    {
        enum Phase
        {
            Menu,
            Intro,
            Investigate,
            Deduce,
            Result
        }

        sealed class ChoiceView
        {
            public int Index;
            public Image Image;
            public Text Label;
        }

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

        static Sprite whiteSprite;

        Font uiFont;
        InvestigationSession session;
        int caseIndex;
        int unlockedThrough;
        int clearedMask;
        bool resultCorrect;
        int? selectedArea;
        int? selectedSuspect;

        GameObject menuPanel;
        LevelButton[] levelButtons;
        GameObject introPanel;
        GameObject investigatePanel;
        GameObject deducePanel;
        GameObject resultPanel;

        Text introChapter;
        Text introBody;
        Text headerTitle;
        Text mapCaption;
        Text pointsText;
        Text promptText;

        TownExplorer explorer;
        QingxiPoster poster;
        int explorerCase = -1;
        GameObject interactDim;
        Text detailTitle;
        Button surfaceButton;
        Button deepButton;
        Text surfaceLabel;
        Text deepLabel;
        Text surfaceBody;
        Text deepBody;
        Text notesHeader;
        ScrollRect notebookScroll;
        Text notebookText;

        ChoiceView[] choices;
        Button confirmButton;

        Image resultStrip;
        Image resultDim;
        Text resultTitle;
        Text resultComment;
        Text resultChoice;
        Text resultTruth;
        Text resultHint;
        Text restartLabel;

        CanvasGroup detailGroup;
        CanvasGroup notebookGroup;
        Coroutine phaseRoutine;
        Coroutine detailRoutine;
        int shownPoints = -1;
        int shownClues = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<QingxiGame>() != null)
                return;

            var go = new GameObject("清溪治理手记");
            go.AddComponent<QingxiGame>();
        }

        void Awake()
        {
            uiFont = LoadUiFont();
            WarmFont();
            caseIndex = 0;
            LoadProgress();
            session = new InvestigationSession(QingxiCases.Get(caseIndex));
            EnsureEventSystem();
            TintCamera();
            BuildUi();
            ApplyCaseChrome();
            Show(Phase.Menu);
        }

        void TintCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Page;
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            menuPanel = BuildMenu(canvasGo.transform);
            introPanel = BuildIntro(canvasGo.transform);
            investigatePanel = BuildInvestigation(canvasGo.transform);
            deducePanel = BuildDeduction(canvasGo.transform);
            resultPanel = BuildResult(canvasGo.transform);
            var posterGo = new GameObject("MenuPoster");
            posterGo.transform.SetParent(transform, false);
            posterGo.SetActive(false);
            poster = posterGo.AddComponent<QingxiPoster>();
            poster.Build(uiFont);
            ApplyCaseChrome();
            PreparePanels();
        }

        GameObject BuildMenu(Transform parent)
        {
            var root = CreatePanel(parent, "Menu", new Color(0f, 0f, 0f, 0f));
            CreateImage(root.transform, "Page", Page, new Vector2(0.48f, 0f), new Vector2(1f, 1f), false);
            var rule = CreateImage(root.transform, "Rule", Gold, new Vector2(0.48f, 0.06f), new Vector2(0.48f, 0.94f), false);
            rule.rectTransform.offsetMax = new Vector2(6f, 0f);

            var sheet = CreateImage(root.transform, "Sheet", Panel, new Vector2(0.54f, 0.08f), new Vector2(0.94f, 0.92f), false);
            CreateText(sheet.transform, "Kicker", "环保专员  ·  现场手记", 18, Gold, TextAnchor.LowerLeft,
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.94f));
            CreateText(sheet.transform, "Title", "清溪治理手记", 48, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.86f), FontStyle.Bold);
            CreateText(sheet.transform, "Lead", "三点调查权限。先看表面，再决定把实锤花在哪里。", 20, Muted, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.60f), new Vector2(0.92f, 0.72f));
            CreateText(sheet.transform, "SheetTitle", "选择关卡", 30, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.60f), FontStyle.Bold);
            CreateText(sheet.transform, "SheetSub", "通关后解锁下一关。", 18, Muted, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.50f));

            levelButtons = new LevelButton[QingxiCases.Count];
            for (int i = 0; i < QingxiCases.Count; i++)
            {
                float top = 0.40f - i * 0.12f;
                float bottom = top - 0.10f;
                int index = i;
                Button button = CreateButton(sheet.transform, "BtnLevel_" + index, "", Card, Paper, 26,
                    new Vector2(0.08f, bottom), new Vector2(0.92f, top));
                button.onClick.AddListener(() => EnterCase(index));
                levelButtons[i] = new LevelButton
                {
                    Index = index,
                    Button = button,
                    Image = button.GetComponent<Image>(),
                    Label = button.GetComponentInChildren<Text>()
                };
            }

            RefreshMenu();
            return root;
        }

        GameObject BuildIntro(Transform parent)
        {
            var root = CreatePanel(parent, "Intro", new Color(0f, 0f, 0f, 0f));
            CreateImage(root.transform, "Dim", new Color(0.10f, 0.16f, 0.13f, 0.28f), Vector2.zero, Vector2.one, true);

            var titleCard = CreateImage(root.transform, "TitleCard", Panel, new Vector2(0.04f, 0.64f), new Vector2(0.38f, 0.92f), false);
            var rule = CreateImage(titleCard.transform, "Rule", Gold, new Vector2(0f, 0.1f), new Vector2(0f, 0.9f), false);
            rule.rectTransform.offsetMax = new Vector2(6f, 0f);
            CreateText(titleCard.transform, "Kicker", "本次任务", 18, Gold, TextAnchor.LowerLeft,
                new Vector2(0.12f, 0.68f), new Vector2(0.92f, 0.9f));
            introChapter = CreateText(titleCard.transform, "Chapter", "", 36, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.12f, 0.28f), new Vector2(0.92f, 0.7f), FontStyle.Bold);
            CreateText(titleCard.transform, "Title", "清溪治理手记", 18, Muted, TextAnchor.UpperLeft,
                new Vector2(0.12f, 0.08f), new Vector2(0.92f, 0.28f));

            var card = CreateImage(root.transform, "Card", Panel, new Vector2(0.46f, 0.12f), new Vector2(0.94f, 0.88f), false);
            introBody = CreateText(card.transform, "Body", "", 28, Paper, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.90f));
            CreateText(card.transform, "Rule",
                "进入小镇后用 WASD 走动，靠近场所按 E。表面观察免费；深入核查每次消耗 1 点行动点。",
                22, Muted, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.56f));

            Button back = CreateButton(card.transform, "BtnIntroMenu", "返回主菜单", Card, Paper, 24,
                new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.22f));
            back.onClick.AddListener(AbandonToMenu);

            Button start = CreateButton(card.transform, "BtnStart", "开始调查", Gold, Ink, 26,
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.22f));
            start.onClick.AddListener(BeginInvestigation);
            return root;
        }

        GameObject BuildInvestigation(Transform parent)
        {
            var root = CreatePanel(parent, "Investigate", new Color(0f, 0f, 0f, 0f));

            var hud = CreateImage(root.transform, "Hud", new Color(0.98f, 0.97f, 0.94f, 0.92f),
                new Vector2(0.02f, 0.90f), new Vector2(0.98f, 0.98f), false);
            headerTitle = CreateText(hud.transform, "Title", "", 28, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0.48f), new Vector2(0.28f, 1f), FontStyle.Bold);
            mapCaption = CreateText(hud.transform, "Caption", "", 16, Muted, TextAnchor.UpperLeft,
                new Vector2(0.02f, 0.05f), new Vector2(0.46f, 0.48f));
            pointsText = CreateText(hud.transform, "Points", "行动点    3 / 3", 22, Gold, TextAnchor.MiddleLeft,
                new Vector2(0.30f, 0f), new Vector2(0.52f, 1f));
            Button abandon = CreateButton(hud.transform, "BtnAbandon", "放弃关卡", Card, Paper, 18,
                new Vector2(0.62f, 0.16f), new Vector2(0.78f, 0.84f));
            abandon.onClick.AddListener(AbandonToMenu);
            Button submit = CreateButton(hud.transform, "BtnSubmitConclusion", "提交结论", Gold, Ink, 18,
                new Vector2(0.80f, 0.16f), new Vector2(0.98f, 0.84f));
            submit.onClick.AddListener(OpenDeduction);

            var promptBg = CreateImage(root.transform, "PromptBg", new Color(0.11f, 0.16f, 0.14f, 0.82f),
                new Vector2(0.32f, 0.03f), new Vector2(0.68f, 0.09f), false);
            promptText = CreateText(promptBg.transform, "Prompt", "WASD 移动    ·    靠近场所后按 E", 20, Ink, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);

            var journal = CreateImage(root.transform, "Journal", new Color(0.98f, 0.97f, 0.94f, 0.9f),
                new Vector2(0.02f, 0.12f), new Vector2(0.28f, 0.40f), false);
            notesHeader = CreateText(journal.transform, "NotesHeader", "线索笔记", 18, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.98f));
            CreateNotebook(journal.transform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.76f));

            var overlay = CreateRect(root.transform, "Interact", Vector2.zero, Vector2.one);
            interactDim = overlay.gameObject;
            Image dim = CreateImage(overlay, "Dim", new Color(0.08f, 0.12f, 0.10f, 0.45f),
                Vector2.zero, Vector2.one, true);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            StyleButton(dimButton);
            dimButton.onClick.AddListener(CloseInteract);

            var card = CreateImage(overlay, "Card", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), true);
            card.rectTransform.sizeDelta = new Vector2(760f, 520f);
            detailGroup = card.gameObject.AddComponent<CanvasGroup>();

            detailTitle = CreateText(card.transform, "AreaTitle", "", 34, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.82f), new Vector2(0.7f, 0.96f), FontStyle.Bold);
            Button leave = CreateButton(card.transform, "BtnLeave", "离开", Card, Paper, 18,
                new Vector2(0.74f, 0.84f), new Vector2(0.94f, 0.95f));
            leave.onClick.AddListener(CloseInteract);

            surfaceButton = CreateButton(card.transform, "BtnSurface", "表面观察 · 免费", SurfaceColor, Ink, 20,
                new Vector2(0.06f, 0.66f), new Vector2(0.48f, 0.80f));
            surfaceLabel = surfaceButton.GetComponentInChildren<Text>();
            surfaceButton.onClick.AddListener(OnSurface);

            deepButton = CreateButton(card.transform, "BtnDeep", "深入核查 · 1点", DeepColor, Ink, 20,
                new Vector2(0.52f, 0.66f), new Vector2(0.94f, 0.80f));
            deepLabel = deepButton.GetComponentInChildren<Text>();
            deepButton.onClick.AddListener(OnDeep);

            CreateText(card.transform, "SurfaceHead", "表面观察", 16, Muted, TextAnchor.LowerLeft,
                new Vector2(0.06f, 0.52f), new Vector2(0.94f, 0.64f));
            surfaceBody = CreateText(card.transform, "SurfaceBody", "", 22, Paper, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.52f));
            CreateText(card.transform, "DeepHead", "深入核查", 16, Gold, TextAnchor.LowerLeft,
                new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.34f));
            deepBody = CreateText(card.transform, "DeepBody", "", 22, Gold, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.22f));

            promptBg.transform.SetAsLastSibling();
            journal.transform.SetAsLastSibling();
            hud.transform.SetAsLastSibling();
            interactDim.SetActive(false);
            return root;
        }

        void CreateNotebook(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var bg = CreateImage(parent, "Notebook", new Color(0.933f, 0.945f, 0.925f, 1f), anchorMin, anchorMax, true);
            notebookGroup = bg.gameObject.AddComponent<CanvasGroup>();
            notebookScroll = bg.gameObject.AddComponent<ScrollRect>();
            notebookScroll.horizontal = false;
            notebookScroll.vertical = true;
            notebookScroll.movementType = ScrollRect.MovementType.Clamped;
            notebookScroll.scrollSensitivity = 40f;
            notebookScroll.inertia = true;

            var viewport = CreateRect(bg.transform, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(14f, 12f);
            viewport.offsetMax = new Vector2(-14f, -12f);
            viewport.gameObject.AddComponent<RectMask2D>();

            notebookText = CreateText(viewport, "Content", "", 20, Muted, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), FontStyle.Normal, VerticalWrapMode.Overflow);
            RectTransform content = notebookText.rectTransform;
            content.pivot = new Vector2(0.5f, 1f);
            var fitter = notebookText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            notebookScroll.viewport = viewport;
            notebookScroll.content = content;
        }

        GameObject BuildDeduction(Transform parent)
        {
            var root = CreatePanel(parent, "Deduce", new Color(0f, 0f, 0f, 0f));
            CreateImage(root.transform, "Dim", new Color(0.08f, 0.12f, 0.10f, 0.34f), Vector2.zero, Vector2.one, true);
            var card = CreateImage(root.transform, "Card", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), false);
            card.rectTransform.sizeDelta = new Vector2(920f, 780f);

            var strip = CreateImage(card.transform, "Strip", Gold, new Vector2(0f, 0f), new Vector2(0f, 1f), false);
            strip.rectTransform.offsetMax = new Vector2(8f, 0f);

            CreateText(card.transform, "Title", "锁定主要污染源", 40, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), FontStyle.Bold);
            CreateText(card.transform, "Sub",
                "根据已有线索选择主因。确认后立即公布答案，也可以返回调查或放弃本关。",
                22, Muted, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.84f));

            choices = new ChoiceView[session.Case.Suspects.Length];
            for (int i = 0; i < session.Case.Suspects.Length; i++)
            {
                float top = 0.70f - i * 0.16f;
                float bottom = top - 0.13f;
                int index = i;
                string label = session.Case.Suspects[i].Label;
                Button button = CreateButton(card.transform, "BtnSuspect_" + index, label, Card, Paper, 28,
                    new Vector2(0.1f, bottom), new Vector2(0.9f, top));
                button.onClick.AddListener(() => SelectSuspect(index));
                choices[i] = new ChoiceView
                {
                    Index = index,
                    Image = button.GetComponent<Image>(),
                    Label = button.GetComponentInChildren<Text>()
                };
            }

            Button giveUp = CreateButton(card.transform, "BtnDeduceMenu", "放弃关卡", Card, Paper, 22,
                new Vector2(0.08f, 0.05f), new Vector2(0.34f, 0.15f));
            giveUp.onClick.AddListener(AbandonToMenu);

            Button back = CreateButton(card.transform, "BtnBackToMap", "返回调查", Card, Paper, 22,
                new Vector2(0.37f, 0.05f), new Vector2(0.63f, 0.15f));
            back.onClick.AddListener(BackToInvestigation);

            confirmButton = CreateButton(card.transform, "BtnConfirmSuspect", "确认结论", Gold, Ink, 22,
                new Vector2(0.66f, 0.05f), new Vector2(0.92f, 0.15f));
            confirmButton.onClick.AddListener(ConfirmSuspect);
            return root;
        }

        GameObject BuildResult(Transform parent)
        {
            var root = CreatePanel(parent, "Result", new Color(0f, 0f, 0f, 0f));
            resultDim = CreateImage(root.transform, "Dim", new Color(0.10f, 0.18f, 0.14f, 0.30f), Vector2.zero, Vector2.one, true);
            var card = CreateImage(root.transform, "Card", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), false);
            card.rectTransform.sizeDelta = new Vector2(880f, 680f);

            resultStrip = CreateImage(card.transform, "Strip", Ok, new Vector2(0f, 0f), new Vector2(0f, 1f), false);
            resultStrip.rectTransform.offsetMax = new Vector2(8f, 0f);

            resultTitle = CreateText(card.transform, "Title", "通关", 48, Ok, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), FontStyle.Bold);
            resultComment = CreateText(card.transform, "Comment", "", 26, Paper, TextAnchor.UpperLeft,
                new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.84f));
            resultChoice = CreateText(card.transform, "Choice", "", 24, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.1f, 0.56f), new Vector2(0.9f, 0.66f));
            resultTruth = CreateText(card.transform, "Truth", "", 24, Gold,
                TextAnchor.MiddleLeft, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.58f));

            CreateText(card.transform, "HintHead", "线索提示", 20, Muted, TextAnchor.LowerLeft,
                new Vector2(0.1f, 0.40f), new Vector2(0.9f, 0.48f));
            resultHint = CreateText(card.transform, "Hint", "", 22, Paper, TextAnchor.UpperLeft,
                new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.40f));

            Button menu = CreateButton(card.transform, "BtnResultMenu", "返回主菜单", Card, Paper, 24,
                new Vector2(0.1f, 0.05f), new Vector2(0.46f, 0.15f));
            menu.onClick.AddListener(AbandonToMenu);

            Button restart = CreateButton(card.transform, "BtnRestart", "重新调查", Gold, Ink, 24,
                new Vector2(0.54f, 0.05f), new Vector2(0.9f, 0.15f));
            restartLabel = restart.GetComponentInChildren<Text>();
            restart.onClick.AddListener(ContinueAfterResult);
            return root;
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
                explorer.MovementLocked = false;
        }

        void OnSurface()
        {
            if (!selectedArea.HasValue)
                return;
            if (!session.TrySurface(selectedArea.Value, out _))
                return;
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
            selectedSuspect = id;
            RefreshSuspects();
        }

        void ConfirmSuspect()
        {
            if (!selectedSuspect.HasValue)
                return;

            resultCorrect = session.IsCorrect(selectedSuspect.Value);
            resultStrip.color = resultCorrect ? Ok : Bad;
            resultTitle.text = resultCorrect ? "通关" : "失败";
            resultTitle.color = resultCorrect ? Ok : Bad;
            resultComment.text = resultCorrect ? session.Case.SuccessText : session.Case.FailText;
            resultChoice.text = "你的结论：" + session.Case.Suspects[selectedSuspect.Value].Label;
            resultTruth.text = "正确答案：" + session.Case.Suspects[session.Case.TruthIndex].Label;
            resultHint.text = session.Case.ClueHint;
            if (resultCorrect)
                MarkCleared(caseIndex);
            if (!resultCorrect)
                restartLabel.text = "重新调查";
            else if (caseIndex < QingxiCases.Count - 1)
                restartLabel.text = "下一关";
            else
                restartLabel.text = "再调查本关";
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

        void ContinueAfterResult()
        {
            if (resultCorrect && caseIndex < QingxiCases.Count - 1)
                caseIndex++;

            DestroyTown();
            session.Bind(QingxiCases.Get(caseIndex));
            selectedArea = null;
            selectedSuspect = null;
            shownPoints = -1;
            shownClues = -1;
            ApplyCaseChrome();
            ShowPreview();
            Show(Phase.Intro);
        }

        void EnterCase(int index)
        {
            if (index > unlockedThrough)
                return;

            caseIndex = index;
            session.Bind(QingxiCases.Get(caseIndex));
            selectedArea = null;
            selectedSuspect = null;
            resultCorrect = false;
            shownPoints = -1;
            shownClues = -1;
            DestroyTown();
            ApplyCaseChrome();
            ShowPreview();
            Show(Phase.Intro);
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
            DestroyTown();
            selectedArea = null;
            selectedSuspect = null;
            RefreshMenu();
            Show(Phase.Menu);
        }

        void ShowTown()
        {
            if (explorer == null || explorerCase != caseIndex)
            {
                DestroyTown();
                var go = new GameObject("TownExplorer");
                go.SetActive(false);
                explorer = go.AddComponent<TownExplorer>();
                explorer.Build(session, uiFont);
                explorer.OnOpen = OpenInteract;
                explorer.OnClose = CloseInteract;
                explorerCase = caseIndex;
            }

            explorer.gameObject.SetActive(true);
            explorer.ResumeExplore();
            CloseInteract();
        }

        void HideTown()
        {
            CloseInteract();
            if (explorer != null)
                explorer.gameObject.SetActive(false);
        }

        void DestroyTown()
        {
            CloseInteract();
            if (explorer != null)
                Destroy(explorer.gameObject);
            explorer = null;
            explorerCase = -1;
        }

        void Update()
        {
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
                    level.Label.text = "第 " + (i + 1) + " 关      " + current.ChapterName + "          未解锁";
                    level.Label.color = Muted;
                }
                else if (cleared)
                {
                    level.Label.text = "第 " + (i + 1) + " 关      " + current.ChapterName + "          已通关";
                    level.Label.color = Gold;
                }
                else
                {
                    level.Label.text = "第 " + (i + 1) + " 关      " + current.ChapterName;
                    level.Label.color = Paper;
                }
            }
        }

        void ApplyCaseChrome()
        {
            CaseDefinition current = session.Case;
            introChapter.text = "第 " + (caseIndex + 1) + " 关  ·  " + current.ChapterName;
            introBody.text = current.IntroBody;
            headerTitle.text = current.PlaceName;
            mapCaption.text = current.MapCaption;
            resultTruth.text = "正确答案：" + current.Suspects[current.TruthIndex].Label;
            resultHint.text = current.ClueHint;

            for (int i = 0; i < choices.Length; i++)
                choices[i].Label.text = current.Suspects[i].Label;
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
            pointsText.text = "行动点    " + session.Points + " / " + QingxiCases.StartingPoints;
        }

        void RefreshDetail()
        {
            if (!selectedArea.HasValue)
                return;

            AreaDefinition def = session.Case.Areas[selectedArea.Value];
            AreaProgress progress = session.GetProgress(selectedArea.Value);
            detailTitle.text = def.Name;

            surfaceLabel.text = progress.SurfaceDone ? "已表面观察" : "表面观察 · 免费";
            surfaceButton.interactable = !progress.SurfaceDone;
            surfaceBody.text = progress.SurfaceDone
                ? def.SurfaceClue
                : "尚未观察。信息偏模糊，可能夹带干扰。";
            surfaceBody.color = progress.SurfaceDone ? Paper : Muted;

            if (progress.DeepDone)
            {
                deepLabel.text = "已深入核查";
                deepButton.interactable = false;
            }
            else if (session.Points < def.DeepCost)
            {
                deepLabel.text = "点数不足 · 需" + def.DeepCost + "点";
                deepButton.interactable = false;
            }
            else
            {
                deepLabel.text = "深入核查 · " + def.DeepCost + "点";
                deepButton.interactable = true;
            }

            deepBody.text = progress.DeepDone
                ? def.DeepClue
                : "尚未核查。关键线索可以直接佐证或排除某个污染源。";
            deepBody.color = progress.DeepDone ? Gold : Muted;
            PlayFade(detailGroup);
        }

        void RefreshNotebook()
        {
            notesHeader.text = session.Clues.Count == 0
                ? "线索笔记"
                : "线索笔记    " + session.Clues.Count;

            if (session.Clues.Count == 0)
            {
                notebookText.color = Muted;
                notebookText.text = "还没有线索。\n先用免费的表面观察判断方向，再把调查点数花在值得深挖的区域。";
            }
            else
            {
                notebookText.color = Paper;
                var sb = new StringBuilder();
                for (int i = 0; i < session.Clues.Count; i++)
                {
                    ClueRecord clue = session.Clues[i];
                    string name = session.Case.Areas[clue.Area].Name;
                    if (clue.IsKey)
                    {
                        sb.Append("<color=#9C4A38>[关键]  ");
                        sb.Append(name);
                        sb.Append('：');
                        sb.Append(clue.Text);
                        sb.Append("</color>\n\n");
                    }
                    else
                    {
                        sb.Append("<color=#1C2824>[普通]  ");
                        sb.Append(name);
                        sb.Append('：');
                        sb.Append(clue.Text);
                        sb.Append("</color>\n\n");
                    }
                }

                notebookText.text = sb.ToString();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(notebookText.rectTransform);
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

        void Show(Phase phase)
        {
            if (poster != null)
                poster.gameObject.SetActive(phase == Phase.Menu);
            if (phaseRoutine != null)
                StopCoroutine(phaseRoutine);
            phaseRoutine = StartCoroutine(PlayTransition(phase));
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
            };
            for (int i = 0; i < panels.Length; i++)
            {
                CanvasGroup group = panels[i].GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
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

            CanvasGroup[] groups =
            {
                menuPanel.GetComponent<CanvasGroup>(),
                introPanel.GetComponent<CanvasGroup>(),
                investigatePanel.GetComponent<CanvasGroup>(),
                deducePanel.GetComponent<CanvasGroup>(),
                resultPanel.GetComponent<CanvasGroup>(),
            };
            float[] start = new float[groups.Length];
            for (int i = 0; i < groups.Length; i++)
                start[i] = groups[i].alpha;

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

                yield return null;
            }

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == to)
                    continue;
                groups[i].alpha = 0f;
                groups[i].blocksRaycasts = false;
                groups[i].gameObject.SetActive(false);
                groups[i].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            to.alpha = 1f;
            to.blocksRaycasts = true;
            toRect.anchoredPosition = Vector2.zero;
            if (poster != null && next != Phase.Menu)
                poster.gameObject.SetActive(false);
            phaseRoutine = null;
        }

        void PlayFade(CanvasGroup group)
        {
            if (group == null)
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
            if (target == null)
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
            string[] names = { "Microsoft YaHei", "SimHei", "SimSun" };
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

        static void StyleButton(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.78f, 0.80f, 0.78f, 0.95f);
            colors.fadeDuration = 0.14f;
            colors.colorMultiplier = 1f;
            button.colors = colors;

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        Button CreateButton(
            Transform parent,
            string name,
            string label,
            Color baseColor,
            Color labelColor,
            int fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Image image = CreateImage(parent, name, baseColor, anchorMin, anchorMax, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            StyleButton(button);
            image.gameObject.AddComponent<UiPress>();
            Text text = CreateText(image.transform, "Label", label, fontSize, labelColor, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = new Vector2(12f, 4f);
            text.rectTransform.offsetMax = new Vector2(-12f, -4f);
            return button;
        }

        GameObject CreatePanel(Transform parent, string name, Color color)
        {
            Image image = CreateImage(parent, name, color, Vector2.zero, Vector2.one, false);
            var group = image.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return image.gameObject;
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        static Image CreateImage(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool raycast)
        {
            RectTransform rt = CreateRect(parent, name, anchorMin, anchorMax);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        Text CreateText(
            Transform parent,
            string name,
            string content,
            int size,
            Color color,
            TextAnchor anchor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            FontStyle style = FontStyle.Normal,
            VerticalWrapMode vertical = VerticalWrapMode.Truncate)
        {
            RectTransform rt = CreateRect(parent, name, anchorMin, anchorMax);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontStyle = style;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.alignByGeometry = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = vertical;
            text.lineSpacing = 1.16f;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        static Sprite WhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return whiteSprite;
        }
    }

    sealed class UiPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        RectTransform rect;
        float rest = 1f;
        float target = 1f;
        float current = 1f;
        float velocity;
        bool over;
        bool down;

        void Awake()
        {
            rect = (RectTransform)transform;
        }

        void Update()
        {
            current = Mathf.SmoothDamp(current, target, ref velocity, 0.07f, Mathf.Infinity, Time.unscaledDeltaTime);
            rect.localScale = new Vector3(current, current, 1f);
        }

        public void SetRest(float scale)
        {
            rest = scale;
            ApplyTarget();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            over = true;
            ApplyTarget();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            over = false;
            down = false;
            ApplyTarget();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            down = true;
            ApplyTarget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            down = false;
            ApplyTarget();
        }

        void ApplyTarget()
        {
            Button button = GetComponent<Button>();
            if (button != null && !button.IsInteractable())
            {
                target = rest;
                return;
            }

            if (down)
                target = rest * 0.96f;
            else if (over)
                target = rest * 1.035f;
            else
                target = rest;
        }
    }
}
