using System;
using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    public enum CellKind
    {
        /// <summary>Not buildable (wall, gap between rooms).</summary>
        None,
        /// <summary>White tiles: shelves, cashier and decorations.</summary>
        ShopFloor,
        /// <summary>Yellow tiles: storage and manufacturing.</summary>
        BackRoom,
        /// <summary>Point of interest outside the grid (the delivery truck).</summary>
        Special,
    }

    public sealed class FloorCell
    {
        public int X;
        public CellKind Kind;
        /// <summary>Identity of the building covering this cell, or null when free.</summary>
        public string Occupant;

        public FloorCell(int x, CellKind kind, string occupant = null)
        {
            X = x;
            Kind = kind;
            Occupant = occupant;
        }
    }

    /// <summary>One horizontal row of the shop, cells sorted by X without gaps.</summary>
    public sealed class FloorRow
    {
        public readonly int Y;
        public readonly List<FloorCell> Cells = new List<FloorCell>();

        public FloorRow(int y) { Y = y; }

        public int MinX => Cells.Count > 0 ? Cells[0].X : 0;
        public int MaxX => Cells.Count > 0 ? Cells[Cells.Count - 1].X : 0;

        public FloorCell At(int x)
        {
            int i = x - MinX;
            return i >= 0 && i < Cells.Count ? Cells[i] : null;
        }
    }

    /// <summary>Cursor movement over the shop floor.</summary>
    public static class FloorNav
    {
        /// <summary>Moves one cell; returns the same x when already at the edge.</summary>
        public static int Step(FloorRow row, int x, int direction)
        {
            if (row == null || row.Cells.Count == 0) return x;
            int target = x + Math.Sign(direction);
            if (target < row.MinX || target > row.MaxX) return Clamp(row, x);
            return target;
        }

        public static int Clamp(FloorRow row, int x)
        {
            if (row == null || row.Cells.Count == 0) return x;
            return Math.Max(row.MinX, Math.Min(row.MaxX, x));
        }

        /// <summary>
        /// Finds the first cell of the next (direction &gt; 0) or previous building, or of a special point,
        /// skipping the building under the cursor. Returns null when there is none.
        /// </summary>
        public static int? NextPointOfInterest(FloorRow row, int x, int direction)
        {
            if (row == null || row.Cells.Count == 0) return null;
            FloorCell here = row.At(x);
            string current = here?.Occupant;
            int dir = Math.Sign(direction);
            if (dir == 0) return null;
            for (int cx = x + dir; cx >= row.MinX && cx <= row.MaxX; cx += dir)
            {
                FloorCell c = row.At(cx);
                bool interesting = c.Kind == CellKind.Special || c.Occupant != null;
                if (!interesting) continue;
                if (c.Occupant != null && c.Occupant == current) continue;
                if (dir < 0 && c.Occupant != null)
                {
                    // Land on the left edge of the previous building.
                    int start = cx;
                    while (start - 1 >= row.MinX && row.At(start - 1).Occupant == c.Occupant) start--;
                    return start;
                }
                return cx;
            }
            return null;
        }

        /// <summary>Stereo position of a cell inside the row: -1 at the left end, +1 at the right end.</summary>
        public static float Pan(FloorRow row, int x)
        {
            if (row == null || row.Cells.Count <= 1) return 0f;
            float t = (float)(x - row.MinX) / (row.MaxX - row.MinX);
            return Math.Max(-1f, Math.Min(1f, t * 2f - 1f));
        }

        /// <summary>
        /// Checks whether a building of <paramref name="size"/> cells starting at <paramref name="x"/>
        /// fits: all cells exist, have the required kind and are free (or belong to <paramref name="ignoreOccupant"/>).
        /// </summary>
        public static PlacementCheck CheckPlacement(FloorRow row, int x, int size, CellKind required, string ignoreOccupant = null)
        {
            if (row == null) return new PlacementCheck(PlacementProblem.OutOfArea, null);
            for (int i = 0; i < Math.Max(1, size); i++)
            {
                FloorCell c = row.At(x + i);
                if (c == null || c.Kind == CellKind.None || c.Kind == CellKind.Special) return new PlacementCheck(PlacementProblem.OutOfArea, null);
                if (c.Kind != required) return new PlacementCheck(PlacementProblem.WrongArea, null);
                if (c.Occupant != null && c.Occupant != ignoreOccupant) return new PlacementCheck(PlacementProblem.Occupied, c.Occupant);
            }
            return new PlacementCheck(PlacementProblem.None, null);
        }

        /// <summary>First x (scanning right from the row start) where the building fits, or null.</summary>
        public static int? FirstFit(FloorRow row, int size, CellKind required, int preferX)
        {
            if (row == null) return null;
            if (CheckPlacement(row, preferX, size, required).Ok) return preferX;
            for (int cx = row.MinX; cx <= row.MaxX; cx++)
            {
                if (CheckPlacement(row, cx, size, required).Ok) return cx;
            }
            return null;
        }
    }

    public enum PlacementProblem
    {
        None,
        OutOfArea,
        WrongArea,
        Occupied,
    }

    public struct PlacementCheck
    {
        public readonly PlacementProblem Problem;
        public readonly string BlockingOccupant;

        public PlacementCheck(PlacementProblem problem, string blocking)
        {
            Problem = problem;
            BlockingOccupant = blocking;
        }

        public bool Ok => Problem == PlacementProblem.None;
    }
}
