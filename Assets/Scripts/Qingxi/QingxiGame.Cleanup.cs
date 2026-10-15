using UnityEngine;

namespace Qingxi
{
    public sealed partial class QingxiGame
    {
        [SerializeField] CleanupGame cleanupGame;
        bool cleanupComplete;
        public bool CleanupInputEnabled => activePhase == Phase.Cleanup && phaseRoutine == null && !loadingScene;

        void BeginCleanup()
        {
            if (!CleanupSave.TryLoad(caseIndex, out var saved)) saved = CleanupSave.Begin(caseIndex);
            cleanupGame.Begin(saved);
            Show(Phase.Cleanup);
        }

        public void CompleteCleanup()
        {
            if (!CleanupInputEnabled || !cleanupGame.Ready) return;
            cleanupComplete = true;
            MarkCleared(caseIndex);
            CleanupSave.Clear(caseIndex);
            InvestigationSave.Clear(caseIndex);
            RefreshResultText();
            Show(Phase.Result);
        }

        bool ResumeCleanup()
        {
            if (!CleanupSave.TryLoad(caseIndex, out var saved)) return false;
            resultCorrect = true;
            selectedSuspect = session.Case.TruthIndex;
            resultStrip.color = resultTitle.color = Ok;
            cleanupGame.Begin(saved);
            Show(Phase.Cleanup, false);
            return true;
        }
    }
}
