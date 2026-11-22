using System;
using System.Collections.Generic;
using System.Text;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// Conversation pictures the game shows without any text: a character's face (its portrait picture) and a
    /// story picture. The portrait file names say what they show ("Portrait_Collegian_Smile"), so the same
    /// character with another picture is a new face, said the way the cashier looks are named.
    /// </summary>
    public static class PortraitFaces
    {
        /// <summary>The character and the variant of a portrait picture: "Portrait_Collegian_Smile" is ("Collegian", "Smile").</summary>
        public static void Split(string pictureName, out string character, out string variant)
        {
            string s = (pictureName ?? string.Empty).Trim();
            if (s.StartsWith("Portrait", StringComparison.OrdinalIgnoreCase)) s = s.Substring("Portrait".Length);
            s = s.Trim(' ', '_', '-');
            int cut = s.IndexOf('_');
            character = (cut < 0 ? s : s.Substring(0, cut)).Trim();
            variant = cut < 0 ? string.Empty : s.Substring(cut + 1).Trim(' ', '_');
        }

        /// <summary>The text key of a variant that shows a face or a look by itself (smiling, surprised), or null.</summary>
        public static string FaceKey(string variant)
        {
            switch (Normalize(variant))
            {
                case "smile": return "face_smile";
                case "surprised": return "face_surprised";
                case "normal": return "face_calm";
                // The otaku after his makeover.
                case "betterthinglasse":
                case "betterthinglasses": return "face_better_glasses";
                default: return null;
            }
        }

        /// <summary>
        /// What to say when the picture at one place goes from <paramref name="before"/> to <paramref name="now"/>
        /// (null: no picture there): a text key, or null for nothing. The same character with another picture is a
        /// new face; a picture that shows a face by itself (smiling, surprised) says it when it appears.
        /// </summary>
        public static string ChangeKey(string before, string now)
        {
            if (string.IsNullOrEmpty(now) || now == before) return null;
            Split(now, out string character, out string variant);
            if (!string.IsNullOrEmpty(before))
            {
                Split(before, out string was, out _);
                if (string.Equals(was, character, StringComparison.OrdinalIgnoreCase))
                {
                    return FaceKey(variant) ?? (variant.Length == 0 ? "face_usual" : "face_changed");
                }
            }
            return FaceKey(variant);
        }

        /// <summary>
        /// The text keys to say for one picture place on a line, in order: its new face (when
        /// <paramref name="faces"/> is on) and that it shakes (when <paramref name="shakes"/> is on). The names are
        /// the pictures before and now (null: none there; a picture fading out counts as none).
        /// </summary>
        public static List<string> NoteKeys(string before, string now, bool shaking, bool faces, bool shakes)
        {
            var keys = new List<string>();
            string face = faces ? ChangeKey(before, now) : null;
            if (face != null) keys.Add(face);
            if (shakes && shaking && !string.IsNullOrEmpty(now)) keys.Add("face_shake");
            return keys;
        }

        /// <summary>A story picture appears, or another one replaces it: the text key, or null.</summary>
        public static string StoryPictureKey(string before, string now) =>
            !string.IsNullOrEmpty(now) && now != before ? "story_picture" : null;

        private static string Normalize(string variant)
        {
            var sb = new StringBuilder();
            foreach (char c in variant ?? string.Empty)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Who a portrait shows, learned from the lines where that portrait is the only one not dimmed (the game
    /// dims the listeners): the speaker heard most often with it wins, so a line where someone else speaks
    /// over an undimmed picture does not decide it.
    /// </summary>
    public sealed class PortraitNames
    {
        private readonly Dictionary<string, Dictionary<string, int>> _counts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        public void Learn(string character, string speaker)
        {
            if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(speaker)) return;
            if (!_counts.TryGetValue(character, out Dictionary<string, int> heard))
            {
                heard = new Dictionary<string, int>(StringComparer.Ordinal);
                _counts[character] = heard;
            }
            heard[speaker] = heard.TryGetValue(speaker, out int n) ? n + 1 : 1;
        }

        /// <summary>The name heard most often with that character's picture (the first one on a tie), or null.</summary>
        public string NameOf(string character)
        {
            if (string.IsNullOrEmpty(character) || !_counts.TryGetValue(character, out Dictionary<string, int> heard)) return null;
            string best = null;
            int most = 0;
            foreach (KeyValuePair<string, int> kv in heard)
            {
                if (kv.Value > most)
                {
                    best = kv.Key;
                    most = kv.Value;
                }
            }
            return best;
        }

        public void Clear() => _counts.Clear();
    }
}
