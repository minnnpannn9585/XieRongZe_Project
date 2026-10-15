using System;
using UnityEngine;

namespace Qingxi
{
    /// <summary>Language-independent, versioned progress for the treatment following a correct verdict.</summary>
    [Serializable]
    public sealed class CleanupState
    {
        public int version = 2;
        public int chapter;
        public int placed;
        public int supports;
        public int[] rotations = new int[4];
        public int[] slotPieces = { -1, -1, -1 };
        public int[] trayOrder = { 0, 1, 2, 3, 4 };
    }

    public static class CleanupSave
    {
        public static string Key(int chapter) => "Qingxi.Cleanup." + chapter;
        public static CleanupState Begin(int chapter)
        {
            var state = new CleanupState { chapter = chapter };
            if (chapter != 1)
                for (int i = state.trayOrder.Length - 1; i > 0; i--)
                {
                    int other = UnityEngine.Random.Range(0, i + 1);
                    (state.trayOrder[i], state.trayOrder[other]) = (state.trayOrder[other], state.trayOrder[i]);
                }
            Store(state);
            return state;
        }
        public static void Store(CleanupState state)
        {
            PlayerPrefs.SetString(Key(state.chapter), JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }
        public static bool TryLoad(int chapter, out CleanupState state) =>
            TryDecode(chapter, PlayerPrefs.GetString(Key(chapter), ""), out state);

        public static bool TryDecode(int chapter, string json, out CleanupState state)
        {
            state = null;
            if (chapter < 0 || chapter > 2 || string.IsNullOrEmpty(json)) return false;
            try
            {
                var saved = JsonUtility.FromJson<CleanupState>(json);
                int full = chapter == 1 ? 15 : 7;
                if (saved == null || (saved.version != 1 && saved.version != 2) || saved.chapter != chapter ||
                    saved.placed < 0 || saved.placed > full || saved.supports < 0 || saved.supports > 3 ||
                    saved.rotations == null || saved.rotations.Length != 4) return false;
                foreach (int rotation in saved.rotations) if (rotation < 0 || rotation > 3) return false;
                if ((chapter != 2 && saved.supports != 0) || (saved.supports != 0 && saved.placed != full)) return false;
                if (saved.version == 1)
                {
                    saved.slotPieces = new[] { -1, -1, -1 };
                    saved.trayOrder = new[] { 0, 1, 2, 3, 4 };
                    if (chapter != 1)
                        for (int i = 0; i < 3; i++) if ((saved.placed & (1 << i)) != 0)
                            saved.slotPieces[i] = CleanupPuzzle.PieceForSlot(chapter, i);
                    saved.supports = 0;
                    saved.version = 2;
                }
                if (saved.supports != 0 || saved.slotPieces == null || saved.slotPieces.Length != 3 ||
                    saved.trayOrder == null || saved.trayOrder.Length != 5) return false;
                int orderMask = 0;
                foreach (int position in saved.trayOrder)
                {
                    if (position < 0 || position > 4 || (orderMask & (1 << position)) != 0) return false;
                    orderMask |= 1 << position;
                }
                int installedMask = 0, usedPieces = 0;
                for (int i = 0; i < saved.slotPieces.Length; i++)
                {
                    int piece = saved.slotPieces[i];
                    if (piece == -1) continue;
                    if (chapter == 1 || piece < 0 || piece >= 5 || (usedPieces & (1 << piece)) != 0 ||
                        CleanupPuzzle.PieceShape(chapter, piece) != CleanupPuzzle.SlotShape(chapter, i)) return false;
                    usedPieces |= 1 << piece;
                    installedMask |= 1 << i;
                }
                if (chapter != 1 && installedMask != saved.placed) return false;
                state = saved;
                return true;
            }
            catch (ArgumentException) { return false; }
        }
        public static void Clear(int chapter) { PlayerPrefs.DeleteKey(Key(chapter)); PlayerPrefs.Save(); }
    }
}
