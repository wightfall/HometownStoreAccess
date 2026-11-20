using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// Turns raw UI strings (TextMeshPro rich text, typing placeholders, odd whitespace) into
    /// something a screen reader can speak cleanly.
    /// </summary>
    public static class TextCleaner
    {
        // TextMeshPro / Unity rich-text tags: <b>, </color>, <color=#fff>, <sprite=3>, <size=120%>, <br> ...
        private static readonly Regex TagRegex = new Regex(
            @"<\s*/?\s*(b|i|u|s|color|size|sprite|font|mark|sup|sub|align|alpha|cspace|indent|line-height|line-indent|link|lowercase|uppercase|smallcaps|margin|mspace|noparse|nobr|page|pos|rotate|style|voffset|width|gradient|material|quad|space|br|strikethrough|underline)\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        // Unity Localization's placeholder for entries missing from a language's table, and the game's own
        // LocalizationManager fallbacks. The key itself is the English text, which is the best we can say.
        private static readonly Regex MissingTranslation = new Regex(@"No translation found for '([^']*)' in \S+", RegexOptions.Compiled);
        private static readonly Regex MissingKey = new Regex(@"Key '([^']*)' not found in table '[^']*'", RegexOptions.Compiled);
        private static readonly Regex RepeatedPunctuation = new Regex(@"([.!?])\s*\1+", RegexOptions.Compiled);

        /// <summary>Removes rich-text markup and normalises whitespace. Returns "" for null.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            // TextMeshPro renders the two-character escapes "\n" and "\t" found in some localized strings.
            string s = raw.Replace("\\n", " ").Replace("\\t", " ").Replace("<br>", " ").Replace("<BR>", " ");
            s = TagRegex.Replace(s, string.Empty);
            if (s.IndexOf("No translation found", StringComparison.Ordinal) >= 0) s = MissingTranslation.Replace(s, "$1");
            if (s.IndexOf("not found in table", StringComparison.Ordinal) >= 0) s = MissingKey.Replace(s, "$1");
            // Zero-width and other invisible characters some fonts use for spacing.
            s = s.Replace("​", string.Empty).Replace("‌", string.Empty).Replace("‍", string.Empty).Replace("﻿", string.Empty);
            s = s.Replace(' ', ' ');
            s = WhitespaceRegex.Replace(s, " ").Trim();
            return FixedText.Translate(s);
        }

        /// <summary>True when the cleaned text carries no speakable information.</summary>
        public static bool IsBlank(string raw)
        {
            string s = Clean(raw);
            if (s.Length == 0) return true;
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }

        /// <summary>
        /// Joins non-blank parts with a separator, skipping duplicates of the previous part
        /// (UI often shows the same text twice, e.g. a shadow label).
        /// </summary>
        public static string Join(string separator, params string[] parts)
        {
            var sb = new StringBuilder();
            string last = null;
            foreach (string p in parts)
            {
                string c = Clean(p);
                if (c.Length == 0 || IsBlank(c)) continue;
                if (last != null && string.Equals(last, c, StringComparison.OrdinalIgnoreCase)) continue;
                if (sb.Length > 0)
                {
                    // "Dear grandchild," + ", " would read as a double comma: keep the text's own punctuation.
                    char end = sb[sb.Length - 1];
                    string trimmedSep = separator.Trim();
                    if (trimmedSep.Length > 0 && IsSentencePunctuation(end)) sb.Append(' ');
                    else sb.Append(separator);
                }
                sb.Append(c);
                last = c;
            }
            return sb.ToString();
        }

        /// <summary>True when the text already ends with a sentence or clause mark.</summary>
        public static bool EndsWithPunctuation(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            char c = s[s.Length - 1];
            return IsSentencePunctuation(c) || c == '…' || c == '~' || c == ')' && s.Length > 1 && IsSentencePunctuation(s[s.Length - 2]);
        }

        private static bool IsSentencePunctuation(char c)
        {
            switch (c)
            {
                case '.':
                case ',':
                case ';':
                case ':':
                case '!':
                case '?':
                case '。':
                case '、':
                case '，':
                case '！':
                case '？':
                case '：':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts a Unity GameObject name such as "Btn_CloseButton (1)" or "nextPhaseButton"
        /// into words ("close button", "next phase button"). Used only as a last resort label.
        /// </summary>
        public static string HumanizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string s = Regex.Replace(name, @"\(\d+\)", string.Empty);
            s = Regex.Replace(s, @"(?i)\b(btn|button|bt|txt|text|img|image|obj|go|ui|panel|prefab|clone)\b", " ");
            s = s.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ');
            // Split camelCase and PascalCase, and letters from digits.
            s = Regex.Replace(s, @"(?<=[a-z])(?=[A-Z])", " ");
            s = Regex.Replace(s, @"(?<=[A-Z])(?=[A-Z][a-z])", " ");
            s = Regex.Replace(s, @"(?<=[A-Za-z])(?=\d)", " ");
            s = Regex.Replace(s, @"(?i)\b(btn|button|bt|txt|text|img|image|obj|go|ui|clone)\b", " ");
            s = WhitespaceRegex.Replace(s, " ").Trim();
            if (s.Length == 0) return string.Empty;
            return CultureInfo.InvariantCulture.TextInfo.ToLower(s);
        }

        /// <summary>Collapses "Text. . ." style repetition that comes from joining several labels.</summary>
        public static string TidyPunctuation(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return RepeatedPunctuation.Replace(s, "$1");
        }
    }
}
