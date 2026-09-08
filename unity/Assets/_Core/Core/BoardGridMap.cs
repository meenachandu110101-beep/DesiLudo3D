using DesiLudo3D.Rules;

namespace DesiLudo3D.Core
{
    /// <summary>15x15 classic Ludo grid coordinates — the canonical mapping
    /// from rules-space (seat, progress) to board cells. Mirrors the Python
    /// reference (tools/reference-engine/ludo/boardmap.py) whose self-check
    /// VALIDATES this table: 52 unique loop cells; starts Red=0/Green=13/
    /// Yellow=26/Blue=39 spaced 13 apart; entrances (start+50) adjacent to
    /// each runway; four documented DIAGONAL corner hops where the classic
    /// board path turns around the centre block.
    /// Grid: (x, y), x = column 0..14 left->right, y = row 0..14 top->bottom.
    /// Pure data + pure math (no UnityEngine).</summary>
    public static class BoardGridMap
    {
        public const int GridSize = 15;

        public static readonly (int X, int Y)[] Loop = new (int, int)[]
        {
            (1, 6), (2, 6), (3, 6), (4, 6), (5, 6),          //  0.. 4  Red's outward run
            (6, 5), (6, 4), (6, 3), (6, 2), (6, 1),          //  5.. 9  up the left arm
            (6, 0), (7, 0), (8, 0),                            // 10..12  top edge
            (8, 1), (8, 2), (8, 3), (8, 4), (8, 5),          // 13..17  Green's outward run
            (9, 6), (10, 6), (11, 6), (12, 6), (13, 6),      // 18..22
            (14, 6), (14, 7), (14, 8),                         // 23..25  right edge
            (13, 8), (12, 8), (11, 8), (10, 8), (9, 8),      // 26..30  Yellow's outward run
            (8, 9), (8, 10), (8, 11), (8, 12), (8, 13),      // 31..35
            (8, 14), (7, 14), (6, 14),                         // 36..38  bottom edge
            (6, 13), (6, 12), (6, 11), (6, 10), (6, 9),      // 39..43  Blue's outward run
            (5, 8), (4, 8), (3, 8), (2, 8), (1, 8),          // 44..48
            (0, 8), (0, 7), (0, 6),                            // 49..51  left edge (50 = Red entrance)
        };

        /// <summary>Home columns: progress 51..55 (5 cells), seat-coloured.</summary>
        public static readonly (int X, int Y)[][] HomeColumns = new (int, int)[][]
        {
            new[] { (1, 7), (2, 7), (3, 7), (4, 7), (5, 7) },            // Red (left arm)
            new[] { (7, 1), (7, 2), (7, 3), (7, 4), (7, 5) },            // Green (top arm)
            new[] { (13, 7), (12, 7), (11, 7), (10, 7), (9, 7) },        // Yellow (right arm)
            new[] { (7, 13), (7, 12), (7, 11), (7, 10), (7, 9) },        // Blue (bottom arm)
        };

        /// <summary>Base (yard) slots — 4 per seat inside each 6x6 corner,
        /// ordered by token index.</summary>
        public static readonly (int X, int Y)[][] BaseSlots = new (int, int)[][]
        {
            new[] { (1, 1), (4, 1), (1, 4), (4, 4) },                    // Red (top-left)
            new[] { (10, 1), (13, 1), (10, 4), (13, 4) },                // Green (top-right)
            new[] { (10, 10), (13, 10), (10, 13), (13, 13) },            // Yellow (bottom-right)
            new[] { (1, 10), (4, 10), (1, 13), (4, 13) },                // Blue (bottom-left)
        };

        /// <summary>Camera azimuths (degrees) that put each seat's corner at
        /// the bottom of the screen (pass-and-play rotation, spec §4.3).</summary>
        public static float SeatAzimuthDeg(Seat seat)
        {
            switch (seat)
            {
                case Seat.Red: return 225f;
                case Seat.Green: return 135f;
                case Seat.Yellow: return 45f;
                case Seat.Blue: return 315f;
                default: return 0f;
            }
        }

        /// <summary>Grid coordinate for a live token. Base tokens have no
        /// loop position (see <see cref="BaseSlot"/>); finished tokens sit in
        /// the centre (7,7).</summary>
        public static (int X, int Y) Position(Seat seat, int progress)
        {
            if (progress == TokenProgress.Base) return BaseSlot(seat, 0); // caller picks real slot
            if (progress >= 0 && progress <= TokenProgress.LoopMax)
                return Loop[(BoardGeometry.StartCell(seat) + progress) % BoardGeometry.LoopCells];
            if (progress >= TokenProgress.ColumnFirst && progress < TokenProgress.Finished)
                return HomeColumns[(int)seat][progress - TokenProgress.ColumnFirst];
            if (progress == TokenProgress.Finished) return (7, 7);
            throw new System.ArgumentOutOfRangeException(nameof(progress));
        }

        public static (int X, int Y) BaseSlot(Seat seat, int tokenIndex) =>
            BaseSlots[(int)seat][tokenIndex];

        /// <summary>Grid -> world (board on the XZ plane, board centre at
        /// origin, one cell = 1 world unit).</summary>
        public static (float X, float Z) GridToWorld(int x, int y) => (x - 7f, y - 7f);
    }
}
