#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Qingxi
{
    public sealed partial class QingxiGame
    {
        /// <summary>Bakes editable UI objects and references into the current scene. Never called in a player.</summary>
        public void EditorBuildUi(int levelIndex)
        {
            if (levelIndex < -1 || levelIndex >= QingxiCases.Count)
                throw new System.ArgumentOutOfRangeException(nameof(levelIndex));
            sceneCaseIndex = levelIndex;
            caseIndex = Mathf.Max(0, levelIndex);
            selectedArea = selectedSuspect = null;
            shownPoints = shownClues = -1;
            session = new InvestigationSession(QingxiCases.Get(caseIndex));
            uiFont = Resources.Load<Font>("Fonts/QingxiUI");
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            explorer = FindAnyObjectByType<TownExplorer>(FindObjectsInactive.Include);
            // Rebuilding is explicit and editor-only; runtime never creates or removes UI.
            Transform previous = transform.Find("Canvas");
            if (previous != null) DestroyImmediate(previous.gameObject);
            EnsureEditorEventSystem();
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (levelIndex < 0)
                menuPanel = BuildMenu(canvasGo.transform);
            else
            {
                introPanel = BuildIntro(canvasGo.transform);
                investigatePanel = BuildInvestigation(canvasGo.transform);
                deducePanel = BuildDeduction(canvasGo.transform);
                resultPanel = BuildResult(canvasGo.transform);
                EditorBuildCleanup(canvasGo.transform, levelIndex);
                BuildJournalOverlay(canvasGo.transform);
                ApplyCaseChrome();
                RefreshInvestigation();
            }
            EditorConfigureLocalization();
            PreparePanels();
            CanvasGroup initial = GroupOf(levelIndex < 0 ? Phase.Menu : Phase.Intro);
            initial.gameObject.SetActive(true);
            initial.alpha = 1f;
            initial.interactable = initial.blocksRaycasts = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        void EnsureEditorEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BindAction(Button button, QingxiUiAction.Action action, int index = -1)
        {
            button.gameObject.AddComponent<QingxiUiAction>().Configure(this, action, index);
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
            CreateText(sheet.transform, "SheetSub", "通关解锁下一关 · 调查自动保存，可随时续玩", 18, Muted, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.50f));

            levelButtons = new LevelButton[QingxiCases.Count];
            for (int i = 0; i < QingxiCases.Count; i++)
            {
                float top = 0.40f - i * 0.12f;
                float bottom = top - 0.10f;
                int index = i;
                Button button = CreateButton(sheet.transform, "BtnLevel_" + index, "", Card, Paper, 26,
                    new Vector2(0.08f, bottom), new Vector2(0.92f, top));
                BindAction(button, QingxiUiAction.Action.EnterCase, index);
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
                "WASD / 方向键移动，靠近场所按 E。\n表面观察免费；深入核查消耗 1 点行动点。\n按 N 展开笔记，Esc 返回。调查自动保存。",
                22, Muted, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.56f));

            Button back = CreateButton(card.transform, "BtnIntroMenu", "返回主菜单", Card, Paper, 24,
                new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.22f));
            BindAction(back, QingxiUiAction.Action.ReturnToMenu);

            Button start = CreateButton(card.transform, "BtnStart", "开始调查", Gold, Ink, 26,
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.22f));
            BindAction(start, QingxiUiAction.Action.BeginInvestigation);
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
            Button abandon = CreateButton(hud.transform, "BtnAbandon", "保存并返回", Card, Paper, 18,
                new Vector2(0.62f, 0.16f), new Vector2(0.78f, 0.84f));
            BindAction(abandon, QingxiUiAction.Action.ReturnToMenu);
            Button submit = CreateButton(hud.transform, "BtnSubmitConclusion", "提交结论", Gold, Ink, 18,
                new Vector2(0.80f, 0.16f), new Vector2(0.98f, 0.84f));
            BindAction(submit, QingxiUiAction.Action.OpenDeduction);

            var promptBg = CreateImage(root.transform, "PromptBg", new Color(0.11f, 0.16f, 0.14f, 0.82f),
                new Vector2(0.32f, 0.03f), new Vector2(0.68f, 0.09f), false);
            promptText = CreateText(promptBg.transform, "Prompt", "WASD 移动    ·    靠近场所后按 E", 20, Ink, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);

            var journal = CreateImage(root.transform, "Journal", new Color(0.98f, 0.97f, 0.94f, 0.9f),
                new Vector2(0.02f, 0.12f), new Vector2(0.28f, 0.40f), false);
            notesHeader = CreateText(journal.transform, "NotesHeader", "线索笔记", 18, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.98f));
            CreateNotebook(journal.transform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.76f));
            Button expand = CreateButton(journal.transform, "BtnExpandNotes", "展开 N", Card, Paper, 16,
                new Vector2(0.65f, 0.80f), new Vector2(0.95f, 0.96f));
            BindAction(expand, QingxiUiAction.Action.ToggleJournal);

            var overlay = CreateRect(root.transform, "Interact", Vector2.zero, Vector2.one);
            interactDim = overlay.gameObject;
            Image dim = CreateImage(overlay, "Dim", new Color(0.08f, 0.12f, 0.10f, 0.45f),
                Vector2.zero, Vector2.one, true);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            StyleButton(dimButton);
            BindAction(dimButton, QingxiUiAction.Action.CloseInteract);

            var card = CreateImage(overlay, "Card", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), true);
            card.rectTransform.sizeDelta = new Vector2(760f, 520f);
            detailGroup = card.gameObject.AddComponent<CanvasGroup>();

            detailTitle = CreateText(card.transform, "AreaTitle", "", 34, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.82f), new Vector2(0.7f, 0.96f), FontStyle.Bold);
            Button leave = CreateButton(card.transform, "BtnLeave", "离开", Card, Paper, 18,
                new Vector2(0.74f, 0.84f), new Vector2(0.94f, 0.95f));
            BindAction(leave, QingxiUiAction.Action.CloseInteract);

            surfaceButton = CreateButton(card.transform, "BtnSurface", "表面观察 · 免费", SurfaceColor, Ink, 20,
                new Vector2(0.06f, 0.66f), new Vector2(0.48f, 0.80f));
            surfaceLabel = surfaceButton.GetComponentInChildren<Text>();
            BindAction(surfaceButton, QingxiUiAction.Action.ObserveSurface);

            deepButton = CreateButton(card.transform, "BtnDeep", "深入核查 · 1点", DeepColor, Ink, 20,
                new Vector2(0.52f, 0.66f), new Vector2(0.94f, 0.80f));
            deepLabel = deepButton.GetComponentInChildren<Text>();
            BindAction(deepButton, QingxiUiAction.Action.InvestigateDeep);

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
            overlay.SetAsLastSibling();
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
                "先查看调查笔记，再选择主因。确认后公布答案；也可以返回继续调查。",
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
                BindAction(button, QingxiUiAction.Action.SelectSuspect, index);
                choices[i] = new ChoiceView
                {
                    Index = index,
                    Image = button.GetComponent<Image>(),
                    Label = button.GetComponentInChildren<Text>()
                };
            }

            Button giveUp = CreateButton(card.transform, "BtnDeduceNotes", "查看笔记 N", Card, Paper, 22,
                new Vector2(0.08f, 0.05f), new Vector2(0.34f, 0.15f));
            BindAction(giveUp, QingxiUiAction.Action.ToggleJournal);

            Button back = CreateButton(card.transform, "BtnBackToMap", "返回调查", Card, Paper, 22,
                new Vector2(0.37f, 0.05f), new Vector2(0.63f, 0.15f));
            BindAction(back, QingxiUiAction.Action.BackToInvestigation);

            confirmButton = CreateButton(card.transform, "BtnConfirmSuspect", "确认结论", Gold, Ink, 22,
                new Vector2(0.66f, 0.05f), new Vector2(0.92f, 0.15f));
            BindAction(confirmButton, QingxiUiAction.Action.ConfirmSuspect);
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
            BindAction(menu, QingxiUiAction.Action.ReturnToMenu);

            Button restart = CreateButton(card.transform, "BtnRestart", "重新调查", Gold, Ink, 24,
                new Vector2(0.54f, 0.05f), new Vector2(0.9f, 0.15f));
            restartLabel = restart.GetComponentInChildren<Text>();
            BindAction(restart, QingxiUiAction.Action.ContinueAfterResult);
            return root;
        }

        void BuildJournalOverlay(Transform parent)
        {
            journalOverlay = CreateRect(parent, "ExpandedJournal", Vector2.zero, Vector2.one).gameObject;
            Image shade = CreateImage(journalOverlay.transform, "Shade", new Color(0.05f, 0.10f, 0.08f, 0.72f),
                Vector2.zero, Vector2.one, true);
            var dismiss = shade.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = shade;
            BindAction(dismiss, QingxiUiAction.Action.CloseJournal);
            Image card = CreateImage(journalOverlay.transform, "Paper", Panel, new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.92f), true);
            CreateImage(card.transform, "Accent", Gold, new Vector2(0f, 0.99f), Vector2.one, false);
            CreateText(card.transform, "Title", "调查笔记", 38, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.85f), new Vector2(0.75f, 0.97f), FontStyle.Bold);
            journalSummary = CreateText(card.transform, "Summary", "", 20, Muted, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.85f));
            Button close = CreateButton(card.transform, "Close", "关闭 Esc", Card, Paper, 20,
                new Vector2(0.76f, 0.88f), new Vector2(0.94f, 0.95f));
            BindAction(close, QingxiUiAction.Action.CloseJournal);
            Image scrollArea = CreateImage(card.transform, "Scroll", Board, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.75f), true);
            expandedScroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            expandedScroll.horizontal = false;
            expandedScroll.movementType = ScrollRect.MovementType.Clamped;
            expandedScroll.scrollSensitivity = 45f;
            RectTransform viewport = CreateRect(scrollArea.transform, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(24f, 20f);
            viewport.offsetMax = new Vector2(-24f, -20f);
            viewport.gameObject.AddComponent<RectMask2D>();
            journalBody = CreateText(viewport, "Content", "", 24, Paper, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), Vector2.one, FontStyle.Normal, VerticalWrapMode.Overflow);
            journalBody.rectTransform.pivot = new Vector2(0.5f, 1f);
            journalBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            expandedScroll.viewport = viewport;
            expandedScroll.content = journalBody.rectTransform;
            CreateText(card.transform, "Tip", "滚轮 / 拖动阅读   ·   关键证据比表面现象更可靠", 18, Muted, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.11f));
            journalOverlay.SetActive(false);
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
            image.sprite = null;
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

    }
}
#endif
