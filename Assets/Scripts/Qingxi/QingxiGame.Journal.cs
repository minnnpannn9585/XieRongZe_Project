using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Qingxi
{
    public sealed partial class QingxiGame
    {
        Phase activePhase;
        GameObject journalOverlay;
        Text journalBody;
        Text journalSummary;
        ScrollRect expandedScroll;

        void BuildJournalOverlay(Transform parent)
        {
            journalOverlay = CreateRect(parent, "ExpandedJournal", Vector2.zero, Vector2.one).gameObject;
            Image shade = CreateImage(journalOverlay.transform, "Shade", new Color(0.05f, 0.10f, 0.08f, 0.72f),
                Vector2.zero, Vector2.one, true);
            var dismiss = shade.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = shade;
            dismiss.onClick.AddListener(CloseJournal);
            Image card = CreateImage(journalOverlay.transform, "Paper", Panel, new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.92f), true);
            CreateImage(card.transform, "Accent", Gold, new Vector2(0f, 0.99f), Vector2.one, false);
            CreateText(card.transform, "Title", "调查笔记", 38, Paper, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.85f), new Vector2(0.75f, 0.97f), FontStyle.Bold);
            journalSummary = CreateText(card.transform, "Summary", "", 20, Muted, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.85f));
            Button close = CreateButton(card.transform, "Close", "关闭 Esc", Card, Paper, 20,
                new Vector2(0.76f, 0.88f), new Vector2(0.94f, 0.95f));
            close.onClick.AddListener(CloseJournal);
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

        void ToggleJournal()
        {
            if (journalOverlay.activeSelf) { CloseJournal(); return; }
            if (phaseRoutine != null || (activePhase != Phase.Investigate && activePhase != Phase.Deduce)) return;
            RefreshNotebook();
            journalBody.text = notebookText.text;
            int deep = 0;
            foreach (ClueRecord clue in session.Clues) if (clue.IsKey) deep++;
            journalSummary.text = session.Case.ChapterName + "   ·   " + session.Clues.Count + " 条线索 / " + deep +
                " 条关键证据   ·   剩余 " + session.Points + " 点";
            journalOverlay.SetActive(true);
            if (explorer != null)
            {
                explorer.MovementLocked = true;
                explorer.InputEnabled = false;
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(journalBody.rectTransform);
            expandedScroll.verticalNormalizedPosition = 1f;
        }

        void CloseJournal()
        {
            if (journalOverlay == null || !journalOverlay.activeSelf) return;
            journalOverlay.SetActive(false);
            if (explorer != null)
            {
                explorer.MovementLocked = activePhase != Phase.Investigate || selectedArea.HasValue;
                explorer.InputEnabled = activePhase == Phase.Investigate && phaseRoutine == null;
            }
        }

        void HandleShortcuts()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || phaseRoutine != null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (journalOverlay.activeSelf) CloseJournal();
                else if (selectedArea.HasValue) CloseInteract();
                else if (activePhase == Phase.Deduce) BackToInvestigation();
                else if (activePhase != Phase.Menu) AbandonToMenu();
            }
            else if (keyboard.nKey.wasPressedThisFrame) ToggleJournal();
        }
    }
}
