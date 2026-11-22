using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// The reward ("Congratulations!!") screen has captions the game leaves in English in every
    /// language. These turn the collected texts of that screen into speech in the mod's language.
    /// </summary>
    public static class RewardText
    {
        /// <summary>One caption in the mod's language; other texts come back unchanged.</summary>
        public static string Translate(string text) => text == null ? null : FixedText.Translate(text.Trim());

        /// <summary>
        /// Reads the screen's texts in order: drops placeholder counters and an item picture's
        /// count when the item's name already says it ("3" next to "Candy x3"), and translates captions.
        /// </summary>
        public static List<string> Prepare(IEnumerable<string> texts)
        {
            var list = new List<string>();
            foreach (string t in texts)
            {
                if (string.IsNullOrEmpty(t) || t == "999" || t == "x999") continue;
                list.Add(t);
            }
            string[] all = list.ToArray();
            list.RemoveAll(t => IsCount(t) && System.Array.Exists(all, o => o != t && o.EndsWith(" x" + t.Trim())));
            for (int i = 0; i < list.Count; i++) list[i] = Translate(list[i]);
            return list;
        }

        private static readonly System.Text.RegularExpressions.Regex NameTimes = new System.Text.RegularExpressions.Regex(@"^(.+?) x(\d+)$");

        /// <summary>"Candy x3" as "Candy: 3" (the "x" is read as a letter). For a reward's item or building line.</summary>
        public static string Quantity(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var m = NameTimes.Match(text);
            return m.Success ? Loc.Get("item_stack", m.Groups[1].Value, m.Groups[2].Value) : text;
        }

        /// <summary>
        /// The budget rows as one sentence. The game shows both rows even when one limit stays the
        /// same (with placeholder text), so only the limits that change are read.
        /// </summary>
        public static string Budget(bool lowestChanges, string lowestFrom, string lowestTo, bool highestChanges, string highestFrom, string highestTo)
        {
            var parts = new List<string>();
            if (lowestChanges) parts.Add(Loc.Get("reward_budget_min", lowestFrom, lowestTo));
            if (highestChanges) parts.Add(Loc.Get("reward_budget_max", highestFrom, highestTo));
            return parts.Count == 0 ? null : string.Join(". ", parts.ToArray());
        }

        private static bool IsCount(string t)
        {
            t = t.Trim();
            if (t.Length == 0) return false;
            foreach (char c in t)
            {
                if (c < '0' || c > '9') return false;
            }
            return true;
        }
    }
}
