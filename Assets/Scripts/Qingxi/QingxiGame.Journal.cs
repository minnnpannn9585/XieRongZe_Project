using UnityEngine;
using L = Qingxi.QingxiLocalization;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Qingxi
{
    public sealed partial class QingxiGame
    {
        Phase activePhase;
        [SerializeField] GameObject journalOverlay;
        [SerializeField] Text journalBody;
        [SerializeField] Text journalSummary;
        [SerializeField] ScrollRect expandedScroll;

        void ToggleJournal()
        {
            if (journalOverlay == null) return;
            if (journalOverlay.activeSelf) { CloseJournal(); return; }
            if (phaseRoutine != null || (activePhase != Phase.Investigate && activePhase != Phase.Deduce)) return;
            RefreshNotebook();
            RefreshJournalContents();
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

        void RefreshJournalContents()
        {
            journalBody.text = notebookText.text;
            int deep = 0;
            foreach (ClueRecord clue in session.Clues) if (clue.IsKey) deep++;
            journalSummary.text = L.Format("{0} · {1} 条线索 / {2} 条关键证据 · 剩余 {3} 点",
                L.Text(session.Case.ChapterName), session.Clues.Count, deep, session.Points);
            LayoutRebuilder.ForceRebuildLayoutImmediate(journalBody.rectTransform);
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
                if (journalOverlay != null && journalOverlay.activeSelf) CloseJournal();
                else if (selectedArea.HasValue) CloseInteract();
                else if (activePhase == Phase.Deduce) BackToInvestigation();
                else if (activePhase != Phase.Menu) AbandonToMenu();
            }
            else if (keyboard.nKey.wasPressedThisFrame) ToggleJournal();
        }
    }
}
