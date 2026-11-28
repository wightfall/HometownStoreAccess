using System;
using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>Stepping through the values of a setting (a choice list) with left and right.</summary>
    public static class ChoiceStep
    {
        /// <summary>
        /// The index one step from <paramref name="index"/> in a list of <paramref name="count"/> values.
        /// With <paramref name="wrap"/> the step goes round from the last value to the first and back;
        /// without it the value stays at the end and <paramref name="atEdge"/> is set.
        /// An unknown current value (index below 0) starts from the first value.
        /// </summary>
        public static int Step(int index, int count, int direction, bool wrap, out bool atEdge)
        {
            atEdge = false;
            if (count <= 0) { atEdge = true; return -1; }
            if (index < 0 || index >= count) return 0;
            int target = index + Math.Sign(direction);
            if (target >= 0 && target < count) return target;
            if (!wrap || count == 1)
            {
                atEdge = true;
                return index;
            }
            return ((target % count) + count) % count;
        }

        /// <summary>
        /// The groups of a navigation list: runs of consecutive items with the same group name. Used to
        /// tell whether Page Up and Page Down do anything on a screen.
        /// </summary>
        public static int GroupCount(IList<string> groups)
        {
            if (groups == null || groups.Count == 0) return 0;
            int n = 1;
            for (int i = 1; i < groups.Count; i++)
            {
                if (!string.Equals(groups[i] ?? string.Empty, groups[i - 1] ?? string.Empty, StringComparison.Ordinal)) n++;
            }
            return n;
        }

        /// <summary>
        /// Whether a list gets heading items: it has at least one named section and at least two
        /// sections in all (a run of items without a name counts as one), so that Page Up and Page Down
        /// have somewhere to go. A list that is one section only keeps its name in front of its items.
        /// </summary>
        public static bool WantsHeadings(IList<string> groups)
        {
            if (groups == null || GroupCount(groups) < 2) return false;
            foreach (string g in groups)
            {
                if (!string.IsNullOrEmpty(g)) return true;
            }
            return false;
        }

        /// <summary>
        /// Where each named section of a list starts and how many items it has (for heading items).
        /// Items without a section get no heading.
        /// </summary>
        public static List<KeyValuePair<int, int>> SectionStarts(IList<string> groups)
        {
            var result = new List<KeyValuePair<int, int>>();
            if (groups == null) return result;
            for (int i = 0; i < groups.Count; i++)
            {
                string g = groups[i] ?? string.Empty;
                if (g.Length == 0 || (i > 0 && string.Equals(groups[i - 1] ?? string.Empty, g, StringComparison.Ordinal))) continue;
                int n = 0;
                for (int k = i; k < groups.Count && string.Equals(groups[k] ?? string.Empty, g, StringComparison.Ordinal); k++) n++;
                result.Add(new KeyValuePair<int, int>(i, n));
            }
            return result;
        }
    }
}
