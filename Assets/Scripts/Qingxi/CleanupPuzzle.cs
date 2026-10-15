namespace Qingxi
{
    /// <summary>Stable silhouettes for authored puzzle equipment and legacy save migration.</summary>
    public static class CleanupPuzzle
    {
        static readonly CleanupShape.Shape[] RiverSlots = { CleanupShape.Shape.Triangle, CleanupShape.Shape.Step, CleanupShape.Shape.House };
        static readonly CleanupShape.Shape[] RiverPieces = { CleanupShape.Shape.Trapezoid, CleanupShape.Shape.House, CleanupShape.Shape.Diamond,
            CleanupShape.Shape.Triangle, CleanupShape.Shape.Step };
        static readonly CleanupShape.Shape[] MineSlots = { CleanupShape.Shape.Diamond, CleanupShape.Shape.Trapezoid, CleanupShape.Shape.Notch };
        static readonly CleanupShape.Shape[] MinePieces = { CleanupShape.Shape.Trapezoid, CleanupShape.Shape.House, CleanupShape.Shape.Notch,
            CleanupShape.Shape.Triangle, CleanupShape.Shape.Diamond };

        public static CleanupShape.Shape SlotShape(int chapter, int slot) => chapter == 1 ? CleanupShape.Shape.Elbow :
            (chapter == 0 ? RiverSlots : MineSlots)[slot];
        public static CleanupShape.Shape PieceShape(int chapter, int piece) => chapter == 1 ? CleanupShape.Shape.Elbow :
            (chapter == 0 ? RiverPieces : MinePieces)[piece];
        public static int PieceForSlot(int chapter, int slot)
        {
            if (chapter == 1) return slot;
            for (int i = 0; i < 5; i++) if (PieceShape(chapter, i) == SlotShape(chapter, slot)) return i;
            return -1;
        }
    }
}
