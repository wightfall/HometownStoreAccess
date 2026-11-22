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
            // A portrait collection entry ("CharacterPortrait_Kid") names its character the same way.
            if (s.StartsWith("CharacterPortrait", StringComparison.OrdinalIgnoreCase)) s = s.Substring("CharacterPortrait".Length);
            else if (s.StartsWith("Portrait", StringComparison.OrdinalIgnoreCase)) s = s.Substring("Portrait".Length);
            s = s.Trim(' ', '_', '-');
            int cut = s.IndexOf('_');
            character = (cut < 0 ? s : s.Substring(0, cut)).Trim();
            variant = cut < 0 ? string.Empty : s.Substring(cut + 1).Trim(' ', '_');
        }

        /// <summary>
        /// The character of a picture or of a portrait collection entry, as a key: "Portrait_Head Villager" and
        /// "CharacterPortrait_HeadVillager" are both "headvillager", and "Portrait_Collegian_Smile" is "collegian".
        /// </summary>
        public static string CharacterKey(string name)
        {
            Split(name, out string character, out _);
            return Normalize(character);
        }

        /// <summary>Both pictures show the same character (another face of it, say).</summary>
        public static bool SameCharacter(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && CharacterKey(a).Length > 0 && CharacterKey(a) == CharacterKey(b);

        /// <summary>The keys from <see cref="NoteKeys"/> say a face (not only that the picture shakes).</summary>
        public static bool HasFace(IList<string> keys)
        {
            foreach (string k in keys)
            {
                if (k != "face_shake") return true;
            }
            return false;
        }

        /// <summary>A name written together as in the game's picture names, as words: "StreetRacer" is "Street Racer".</summary>
        public static string SpacedWords(string name)
        {
            var sb = new StringBuilder();
            string s = name ?? string.Empty;
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
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
                if (SameCharacter(before, now))
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

        /// <summary>The text key for characters whose pictures fade out (they leave): one, or several at once.</summary>
        public static string LeaveKey(int count) => count > 1 ? "face_leave_many" : "face_leave";

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
    /// Who a portrait shows, learned from the conversations' lines. The game dims the listeners, so on a line with
    /// only one picture not dimmed, that picture is the speaker's. The player and the narrator also speak over
    /// pictures, but mostly with none lit: such speakers never count as a character's name. Of the rest, the
    /// speaker heard most often with a character's picture wins.
    /// </summary>
    public sealed class PortraitNames
    {
        private readonly Dictionary<string, Dictionary<string, int>> _heard = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        // For each speaker: the lines heard, and those with exactly one picture lit.
        private readonly Dictionary<string, int[]> _speakers = new Dictionary<string, int[]>(StringComparer.Ordinal);

        /// <summary>One line: who speaks, and the character of the only picture not dimmed (null: none, or several).</summary>
        public void LearnLine(string speaker, string litCharacter)
        {
            if (string.IsNullOrEmpty(speaker)) return;
            if (!_speakers.TryGetValue(speaker, out int[] lines))
            {
                lines = new int[2];
                _speakers[speaker] = lines;
            }
            lines[0]++;
            if (string.IsNullOrEmpty(litCharacter)) return;
            lines[1]++;
            if (!_heard.TryGetValue(litCharacter, out Dictionary<string, int> heard))
            {
                heard = new Dictionary<string, int>(StringComparer.Ordinal);
                _heard[litCharacter] = heard;
            }
            heard[speaker] = heard.TryGetValue(speaker, out int n) ? n + 1 : 1;
        }

        /// <summary>
        /// A speaker mostly heard with no single picture lit: the player or the narrator (or someone without a
        /// picture), not a character whose picture shows.
        /// </summary>
        public bool IsGeneric(string speaker) =>
            !string.IsNullOrEmpty(speaker) && _speakers.TryGetValue(speaker, out int[] n) && n[0] >= 2 && n[1] * 2 < n[0];

        /// <summary>The character's picture was the one lit on a line of <paramref name="speaker"/>'s at least once.</summary>
        public bool HasHeard(string character, string speaker) =>
            !string.IsNullOrEmpty(character) && !string.IsNullOrEmpty(speaker) && _heard.TryGetValue(character, out Dictionary<string, int> heard) && heard.ContainsKey(speaker);

        /// <summary>The name heard most often with that character's picture, not counting the player or the narrator; or null.</summary>
        public string NameOf(string character) => NameOf(character, IsGeneric);

        /// <summary>
        /// As <see cref="NameOf(string)"/>, with another judge of who the player and the narrator are (a list of
        /// one conversation knows too little for that).
        /// </summary>
        public string NameOf(string character, Func<string, bool> isGeneric)
        {
            if (string.IsNullOrEmpty(character) || !_heard.TryGetValue(character, out Dictionary<string, int> heard)) return null;
            string best = null;
            int most = 0;
            foreach (KeyValuePair<string, int> kv in heard)
            {
                if (kv.Value > most && !isGeneric(kv.Key))
                {
                    best = kv.Key;
                    most = kv.Value;
                }
            }
            return best;
        }

        public void Clear()
        {
            _heard.Clear();
            _speakers.Clear();
        }
    }
}
