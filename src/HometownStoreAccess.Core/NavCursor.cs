using System;
using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>Result of a cursor movement, used to choose feedback (speech, wrap sound, edge bump).</summary>
    public enum MoveResult
    {
        Moved,
        Wrapped,
        AtEdge,
        Empty,
    }

    /// <summary>
    /// Index bookkeeping for a list of focusable items that is rebuilt frequently (the game destroys
    /// and recreates most of its UI rows on every refresh). Focus is kept on the same logical item
    /// by key whenever possible.
    /// </summary>
    public sealed class NavCursor
    {
        public int Index { get; private set; } = -1;
        public int Count { get; private set; }
        public bool Wrap { get; set; } = true;
        public string FocusedKey { get; private set; }

        /// <summary>
        /// Replaces the item set. <paramref name="keys"/> are stable identifiers of the new items in order.
        /// Returns true when the focused item changed identity (and therefore should be re-announced).
        /// </summary>
        public bool Reset(IList<string> keys, bool preferFirst = false)
        {
            string previousKey = FocusedKey;
            int previousIndex = Index;
            Count = keys.Count;
            if (Count == 0)
            {
                Index = -1;
                FocusedKey = null;
                return previousKey != null;
            }
            int found = -1;
            if (!preferFirst && previousKey != null)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    if (string.Equals(keys[i], previousKey, StringComparison.Ordinal)) { found = i; break; }
                }
            }
            if (found >= 0)
            {
                Index = found;
            }
            else if (preferFirst || previousIndex < 0)
            {
                Index = 0;
            }
            else
            {
                Index = Math.Min(previousIndex, Count - 1);
            }
            FocusedKey = keys[Index];
            return !string.Equals(previousKey, FocusedKey, StringComparison.Ordinal);
        }

        public MoveResult Move(int delta, IList<string> keys)
        {
            if (Count == 0) return MoveResult.Empty;
            int target = Index + delta;
            MoveResult result = MoveResult.Moved;
            if (target < 0 || target >= Count)
            {
                if (!Wrap || Count == 1)
                {
                    target = Math.Max(0, Math.Min(Count - 1, target));
                    Index = target;
                    FocusedKey = keys[Index];
                    return MoveResult.AtEdge;
                }
                target = ((target % Count) + Count) % Count;
                result = MoveResult.Wrapped;
            }
            Index = target;
            FocusedKey = keys[Index];
            return result;
        }

        public MoveResult MoveTo(int index, IList<string> keys)
        {
            if (Count == 0) return MoveResult.Empty;
            int clamped = Math.Max(0, Math.Min(Count - 1, index));
            bool same = clamped == Index;
            Index = clamped;
            FocusedKey = keys[Index];
            return same ? MoveResult.AtEdge : MoveResult.Moved;
        }

        /// <summary>
        /// Moves to the first item of the next (or previous) group, where <paramref name="groups"/>
        /// gives each item's group name. Wraps around when <see cref="Wrap"/> is set; otherwise it stops
        /// at the first and the last group.
        /// </summary>
        public MoveResult MoveGroup(int direction, IList<string> groups, IList<string> keys)
        {
            if (Count == 0) return MoveResult.Empty;
            // Groups are runs of consecutive items with the same group name.
            var starts = new List<int>();
            for (int i = 0; i < Count; i++)
            {
                if (i == 0 || !string.Equals(groups[i] ?? string.Empty, groups[i - 1] ?? string.Empty, StringComparison.Ordinal)) starts.Add(i);
            }
            int current = 0;
            for (int g = 0; g < starts.Count; g++)
            {
                if (starts[g] <= Index) current = g;
            }
            int target;
            MoveResult result = MoveResult.Moved;
            if (direction > 0)
            {
                if (starts.Count == 1) return MoveResult.AtEdge;
                if (current + 1 < starts.Count) target = starts[current + 1];
                else if (!Wrap) return MoveResult.AtEdge;
                else { target = starts[0]; result = MoveResult.Wrapped; }
            }
            else
            {
                if (Index != starts[current]) target = starts[current];
                else if (starts.Count == 1) return MoveResult.AtEdge;
                else if (current > 0) target = starts[current - 1];
                else if (!Wrap) return MoveResult.AtEdge;
                else { target = starts[starts.Count - 1]; result = MoveResult.Wrapped; }
            }
            Index = target;
            FocusedKey = keys[Index];
            return result;
        }

        /// <summary>
        /// Where the focus goes when the focused item has gone from a rebuilt list (goods moved away, a
        /// basket line removed): the nearest item of the old list that is still there, first within the
        /// vanished item's own section (one step after it, then one before, and so on), then anywhere.
        /// Headings are skipped. Returns its index in <paramref name="newKeys"/>, or -1 when none is left.
        /// </summary>
        public static int NearestSurvivor(IList<string> oldKeys, IList<string> oldGroups, int oldIndex, IList<string> newKeys, Func<string, bool> isHeading)
        {
            if (oldKeys == null || newKeys == null || oldIndex < 0 || oldIndex >= oldKeys.Count) return -1;
            var where = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < newKeys.Count; i++)
            {
                if (newKeys[i] != null && !where.ContainsKey(newKeys[i])) where[newKeys[i]] = i;
            }
            string group = oldGroups != null && oldIndex < oldGroups.Count ? oldGroups[oldIndex] ?? string.Empty : string.Empty;
            // The vanished item's own section: the run of neighbours with the same section name around it
            // (items far away with the same name, such as unnamed ones at the end, are another section).
            int runStart = oldIndex, runEnd = oldIndex;
            if (oldGroups != null && oldGroups.Count == oldKeys.Count)
            {
                while (runStart > 0 && string.Equals(oldGroups[runStart - 1] ?? string.Empty, group, StringComparison.Ordinal)) runStart--;
                while (runEnd < oldKeys.Count - 1 && string.Equals(oldGroups[runEnd + 1] ?? string.Empty, group, StringComparison.Ordinal)) runEnd++;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int d = 1; d < oldKeys.Count; d++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        int j = side == 0 ? oldIndex + d : oldIndex - d;
                        if (j < 0 || j >= oldKeys.Count) continue;
                        if (pass == 0 && (j < runStart || j > runEnd)) continue;
                        string k = oldKeys[j];
                        if (k == null || (isHeading != null && isHeading(k))) continue;
                        if (where.TryGetValue(k, out int at)) return at;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// First-letter navigation: finds the next item after the current one whose label starts with
        /// <paramref name="prefix"/> (case-insensitive), wrapping around. Returns false if none.
        /// </summary>
        public bool FindNextByPrefix(string prefix, IList<string> labels, IList<string> keys)
        {
            if (Count == 0 || string.IsNullOrEmpty(prefix)) return false;
            for (int step = 1; step <= Count; step++)
            {
                int j = (Index + step) % Count;
                string label = labels[j] ?? string.Empty;
                if (label.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                {
                    Index = j;
                    FocusedKey = keys[Index];
                    return true;
                }
            }
            return false;
        }
    }
}
