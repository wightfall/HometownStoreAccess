using System;
using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// A category filter stepped with Ctrl+Up and Ctrl+Down in list screens: "everything" first, then the
    /// categories present. Categories are paths: "item" covers "item:Drink" and "item:FoodAndSnack", so a
    /// screen can offer "all goods" next to the single goods categories.
    /// </summary>
    public sealed class CategoryCycle
    {
        public const string All = "*";

        private readonly List<string> _options = new List<string> { All };

        public string Current { get; private set; } = All;

        public IReadOnlyList<string> Options => _options;

        /// <summary>Position of the current option, 1-based, for "3 of 7".</summary>
        public int Position => Math.Max(0, _options.IndexOf(Current)) + 1;

        public int Count => _options.Count;

        /// <summary>
        /// Replaces the available categories (keeping their order, dropping duplicates and empties).
        /// The current choice is kept while it still exists, otherwise the filter falls back to everything.
        /// </summary>
        public void SetOptions(IEnumerable<string> categories)
        {
            _options.Clear();
            _options.Add(All);
            if (categories != null)
            {
                foreach (string c in categories)
                {
                    if (string.IsNullOrEmpty(c) || c == All || _options.Contains(c)) continue;
                    _options.Add(c);
                }
            }
            if (!_options.Contains(Current)) Current = All;
        }

        /// <summary>Moves to the previous (-1) or next (+1) option, wrapping around; returns the new option.</summary>
        public string Step(int direction)
        {
            if (direction == 0) return Current;
            int n = _options.Count;
            int i = Math.Max(0, _options.IndexOf(Current));
            i = ((i + Math.Sign(direction)) % n + n) % n;
            Current = _options[i];
            return Current;
        }

        public void Reset() => Current = All;

        /// <summary>True when something in <paramref name="category"/> is shown under the current option.</summary>
        public bool Matches(string category) => Covers(Current, category);

        /// <summary>True when <paramref name="option"/> includes <paramref name="category"/> ("item" includes "item:Drink").</summary>
        public static bool Covers(string option, string category)
        {
            if (option == All) return true;
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(option)) return false;
            if (category == option) return true;
            return category.Length > option.Length && category[option.Length] == ':' && category.StartsWith(option, StringComparison.Ordinal);
        }

        /// <summary>How many of the given categories fall under <paramref name="option"/>.</summary>
        public static int CountFor(string option, IEnumerable<string> categories)
        {
            int n = 0;
            foreach (string c in categories) if (Covers(option, c)) n++;
            return n;
        }

        /// <summary>
        /// The useful options for a list of category paths, in first-seen order: every top level when there
        /// are several ("all goods", "all buildings"), and the sub-categories of a top level that has more
        /// than one. An option that would show the same as "everything" is left out.
        /// </summary>
        public static List<string> OptionsFor(IEnumerable<string> categories)
        {
            var tops = new List<string>();
            var subs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string c in categories)
            {
                if (string.IsNullOrEmpty(c)) continue;
                int colon = c.IndexOf(':');
                string top = colon < 0 ? c : c.Substring(0, colon);
                if (!subs.TryGetValue(top, out List<string> list))
                {
                    list = new List<string>();
                    subs[top] = list;
                    tops.Add(top);
                }
                if (colon >= 0 && !list.Contains(c)) list.Add(c);
            }
            var result = new List<string>();
            foreach (string top in tops)
            {
                if (tops.Count > 1) result.Add(top);
                if (subs[top].Count > 1) result.AddRange(subs[top]);
            }
            return result;
        }
    }
}
