using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// The plain-text translation format. One entry per line: <c>key = text</c>. Lines starting with
    /// <c>#</c> are comments, <c>@name = ...</c> and <c>@base = ...</c> are settings. In texts,
    /// <c>\n</c> is a line break and <c>{0}</c>, <c>{1}</c> are values the mod fills in.
    /// Mod text files use the mod's keys; game text files use <c>Table/Key</c> from the game's tables.
    /// </summary>
    public sealed class LangFile
    {
        public readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Errors = new List<string>();

        public string Name => Settings.TryGetValue("name", out string n) ? n : null;

        /// <summary>The game language whose text is shown underneath a game text translation (default English).</summary>
        public string BaseLocale => Settings.TryGetValue("base", out string b) && b.Length > 0 ? b : "en";

        public static LangFile Parse(string text)
        {
            var f = new LangFile();
            if (string.IsNullOrEmpty(text)) return f;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (i == 0 && line.Length > 0 && line[0] == '\uFEFF') line = line.Substring(1).Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    f.Errors.Add("line " + (i + 1) + ": missing '='");
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = Unescape(line.Substring(eq + 1).Trim());
                if (key.StartsWith("@", StringComparison.Ordinal)) f.Settings[key.Substring(1)] = value;
                else if (value.Length > 0) f.Entries[key] = value;
            }
            return f;
        }

        public static string Unescape(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");
        public static string Escape(string s) => (s ?? string.Empty).Replace("\r", string.Empty).Replace("\n", "\\n").Replace("\t", "\\t");

        /// <summary>A template listing every key with the English text as a comment and an empty value.</summary>
        public static string BuildTemplate(string header, IEnumerable<KeyValuePair<string, string>> englishEntries)
        {
            var sb = new StringBuilder();
            foreach (string h in header.Replace("\r", string.Empty).Split('\n')) sb.Append("# ").AppendLine(h);
            sb.AppendLine("@name = ");
            sb.AppendLine();
            foreach (var kv in englishEntries)
            {
                sb.Append("# ").AppendLine(Escape(kv.Value));
                sb.Append(kv.Key).AppendLine(" = ");
            }
            return sb.ToString();
        }

        public static LangFile Load(string path)
        {
            try
            {
                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (IOException e)
            {
                var f = new LangFile();
                f.Errors.Add(e.Message);
                return f;
            }
        }
    }
}
