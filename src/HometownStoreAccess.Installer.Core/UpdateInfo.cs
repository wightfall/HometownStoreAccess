using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HometownStoreAccess.Installer
{
    /// <summary>The latest GitHub release, parsed from the releases API response.</summary>
    public sealed class ReleaseInfo
    {
        public string Tag;
        public string Version;
        public string Notes;
        public string InstallerUrl;
        public string PageUrl;
    }

    /// <summary>What the installer's main button does.</summary>
    public enum InstallAction
    {
        Install,
        Update,
        Reinstall,
        /// <summary>This installer carries an older version than the one installed.</summary>
        InstallOlder,
    }

    public static class UpdateInfo
    {
        public const string Repository = "wightfall/HometownStoreAccess";
        public const string LatestApi = "https://api.github.com/repos/" + Repository + "/releases/latest";
        public const string InstallerAssetName = "HometownStoreAccessInstaller.exe";

        /// <summary>Extracts tag, notes and the installer download link from the API JSON (no JSON library needed).</summary>
        public static ReleaseInfo Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string tag = JsonString(json, "tag_name");
            if (tag == null) return null;
            var info = new ReleaseInfo
            {
                Tag = tag,
                Version = NormalizeVersion(tag),
                Notes = JsonString(json, "body") ?? string.Empty,
                PageUrl = JsonString(json, "html_url"),
            };
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                string url = Unescape(m.Groups[1].Value);
                if (url.EndsWith("/" + InstallerAssetName, StringComparison.OrdinalIgnoreCase)) info.InstallerUrl = url;
            }
            return info;
        }

        private static string JsonString(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Unescape(m.Groups[1].Value) : null;
        }

        /// <summary>Decodes JSON string escapes (\n, \", \\, \uXXXX).</summary>
        public static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length)
                {
                    sb.Append(c);
                    continue;
                }
                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': break;
                    case 't': sb.Append('\t'); break;
                    case 'b': case 'f': break;
                    case 'u':
                        if (i + 4 < s.Length && int.TryParse(s.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

        public static string NormalizeVersion(string tag) => (tag ?? string.Empty).Trim().TrimStart('v', 'V');

        /// <summary>True when <paramref name="candidate"/> is a higher version than <paramref name="current"/>.</summary>
        public static bool IsNewer(string candidate, string current)
        {
            if (!TryParse(candidate, out Version a)) return false;
            if (!TryParse(current, out Version b)) return true;
            return a > b;
        }

        /// <summary>-1, 0 or 1 as <paramref name="a"/> is lower, the same or higher; null when either is not a version.</summary>
        public static int? Compare(string a, string b)
        {
            if (!TryParse(a, out Version va) || !TryParse(b, out Version vb)) return null;
            return Math.Sign(Normalize(va).CompareTo(Normalize(vb)));
        }

        // 1.5 and 1.5.0 are the same version.
        private static Version Normalize(Version v) => new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));

        /// <summary>
        /// Whether to offer the latest release: only when it is newer than both the mod that is installed
        /// and the one this installer carries (an old installer run on an up-to-date mod offers nothing).
        /// </summary>
        public static bool ShouldOffer(string latest, string installed, string payload)
        {
            if (!IsNewer(latest, payload ?? "0")) return false;
            return installed == null || Compare(installed, "0") == null || IsNewer(latest, installed);
        }

        /// <summary>What the Install button does, from the installed version (null: none) and this installer's.</summary>
        public static InstallAction ActionFor(bool installed, string installedVersion, string payloadVersion)
        {
            if (!installed) return InstallAction.Install;
            int? c = Compare(payloadVersion, installedVersion);
            if (c == null) return InstallAction.Update;
            return c > 0 ? InstallAction.Update : c == 0 ? InstallAction.Reinstall : InstallAction.InstallOlder;
        }

        private static bool TryParse(string s, out Version v)
        {
            v = null;
            string n = NormalizeVersion(s);
            int dash = n.IndexOfAny(new[] { '-', '+', ' ' });
            if (dash > 0) n = n.Substring(0, dash);
            if (n.Length == 0) return false;
            if (n.IndexOf('.') < 0) n += ".0";
            return Version.TryParse(n, out v);
        }

        /// <summary>Release notes as separate lines for the line-by-line "what's new" box.</summary>
        public static List<string> NoteLines(string notes)
        {
            var lines = new List<string>();
            foreach (string raw in (notes ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                string l = raw.Trim();
                if (l.StartsWith("#", StringComparison.Ordinal))
                {
                    l = l.TrimStart('#').Trim();
                    // How to download and run the installer: it is already running.
                    if (string.Equals(l, "Install", StringComparison.OrdinalIgnoreCase)) break;
                }
                if (l.StartsWith("- ", StringComparison.Ordinal) || l.StartsWith("* ", StringComparison.Ordinal)) l = l.Substring(2);
                l = l.Replace("**", string.Empty).Replace("`", string.Empty);
                if (l.Length > 0) lines.Add(l);
            }
            return lines;
        }
    }

    /// <summary>Edits one value in a BepInEx .cfg file (INI style), creating the file or section if needed.</summary>
    public static class ConfigEditor
    {
        public static void SetValue(string path, string section, string key, string value)
        {
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            int sectionStart = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase)) { sectionStart = i; break; }
            }
            if (sectionStart < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add(string.Empty);
                lines.Add("[" + section + "]");
                lines.Add(key + " = " + value);
            }
            else
            {
                int insertAt = lines.Count;
                bool done = false;
                for (int i = sectionStart + 1; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("[", StringComparison.Ordinal)) { insertAt = i; break; }
                    int eq = t.IndexOf('=');
                    if (eq > 0 && !t.StartsWith("#", StringComparison.Ordinal) && t.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = key + " = " + value;
                        done = true;
                        break;
                    }
                }
                if (!done) lines.Insert(insertAt, key + " = " + value);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
        }

        public static string ModConfigPath(string gameDir) => Path.Combine(gameDir, "BepInEx", "config", "com.wightfall.hometownstoreaccess.cfg");
    }
}
