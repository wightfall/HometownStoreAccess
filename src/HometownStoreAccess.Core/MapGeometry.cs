using System;
using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>Grid coordinate on the travel map board.</summary>
    public struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X;
        public readonly int Y;

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint p && Equals(p);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => X + "," + Y;
    }

    /// <summary>Path distances and directions on the map board (a grid of walkable tiles).</summary>
    public static class MapGeometry
    {
        private static readonly GridPoint[] Neighbours = { new GridPoint(0, 1), new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0) };

        /// <summary>Number of steps from <paramref name="start"/> to <paramref name="target"/> over the walkable set, or null if unreachable.</summary>
        public static int? StepsBetween(ICollection<GridPoint> walkable, GridPoint start, GridPoint target)
        {
            if (walkable == null || !walkable.Contains(start) || !walkable.Contains(target)) return null;
            if (start.Equals(target)) return 0;
            var set = walkable as HashSet<GridPoint> ?? new HashSet<GridPoint>(walkable);
            var dist = new Dictionary<GridPoint, int> { [start] = 0 };
            var queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                int d = dist[p];
                foreach (GridPoint n in Neighbours)
                {
                    var q = new GridPoint(p.X + n.X, p.Y + n.Y);
                    if (!set.Contains(q) || dist.ContainsKey(q)) continue;
                    if (q.Equals(target)) return d + 1;
                    dist[q] = d + 1;
                    queue.Enqueue(q);
                }
            }
            return null;
        }

        /// <summary>Walkable grid neighbours of a point.</summary>
        public static List<GridPoint> NeighboursOf(ICollection<GridPoint> walkable, GridPoint p)
        {
            var list = new List<GridPoint>();
            foreach (GridPoint n in Neighbours)
            {
                var q = new GridPoint(p.X + n.X, p.Y + n.Y);
                if (walkable.Contains(q)) list.Add(q);
            }
            return list;
        }

        /// <summary>
        /// Localization key of the 8-way compass direction of a screen-space offset (up = north),
        /// or null when the offset is negligible.
        /// </summary>
        public static string CompassKey(double dx, double dy, double minLength = 1.0)
        {
            if (Math.Sqrt(dx * dx + dy * dy) < minLength) return null;
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI; // 0 = east, 90 = north
            if (angle < 0) angle += 360.0;
            int sector = (int)Math.Round(angle / 45.0) % 8;
            switch (sector)
            {
                case 0: return "dir_e";
                case 1: return "dir_ne";
                case 2: return "dir_n";
                case 3: return "dir_nw";
                case 4: return "dir_w";
                case 5: return "dir_sw";
                case 6: return "dir_s";
                default: return "dir_se";
            }
        }

        /// <summary>
        /// Of several candidate screen offsets, picks the index that best matches the wanted direction
        /// (dot product of unit vectors above <paramref name="minCos"/>), or -1 when none does.
        /// </summary>
        public static int BestMatch(IList<(double dx, double dy)> candidates, double wantX, double wantY, double minCos = 0.35)
        {
            int best = -1;
            double bestDot = minCos;
            double wl = Math.Sqrt(wantX * wantX + wantY * wantY);
            if (wl <= 0) return -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                var (dx, dy) = candidates[i];
                double l = Math.Sqrt(dx * dx + dy * dy);
                if (l <= 0) continue;
                double dot = (dx * wantX + dy * wantY) / (l * wl);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = i;
                }
            }
            return best;
        }
    }
}
