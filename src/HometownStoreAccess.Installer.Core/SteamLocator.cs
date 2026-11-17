using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HometownStoreAccess.Installer
{
    /// <summary>Finds the Hometown Store installation through Steam's library folders.</summary>
    public static class SteamLocator
    {
        public const string AppId = "4643920";
        public const string GameFolderName = "HometownStore";
        public const string GameExe = "HometownStore.exe";

        private static readonly Regex PathEntry = new Regex("\"path\"\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex OldEntry = new Regex("^\\s*\"\\d+\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex InstallDir = new Regex("\"installdir\"\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Library paths listed in a libraryfolders.vdf file (both the old and new formats).</summary>
        public static List<string> ParseLibraryFolders(string vdf)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(vdf)) return result;
            foreach (Match m in PathEntry.Matches(vdf))
            {
                AddUnique(result, Unescape(m.Groups[1].Value));
            }
            // Old format, one library per line: "1" "D:\\SteamLibrary". Matching whole lines keeps numeric
            // values such as "TimeNextStatsReport" "1234567" from swallowing the next entry.
            foreach (Match m in OldEntry.Matches(vdf))
            {
                string p = Unescape(m.Groups[1].Value);
                if (p.IndexOf(':') > 0 || p.StartsWith("/", StringComparison.Ordinal)) AddUnique(result, p);
            }
            return result;
        }

        /// <summary>The install folder name from an appmanifest_*.acf file, or null.</summary>
        public static string ParseInstallDir(string acf)
        {
            if (string.IsNullOrEmpty(acf)) return null;
            Match m = InstallDir.Match(acf);
            return m.Success ? Unescape(m.Groups[1].Value) : null;
        }

        private static string Unescape(string s) => s.Replace("\\\\", "\\").Replace("\\\"", "\"");

        private static void AddUnique(List<string> list, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            foreach (string existing in list)
            {
                if (string.Equals(existing.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return;
            }
            list.Add(path);
        }

        /// <summary>
        /// The Steam app id of the game installed in this folder (the demo, or a later full release), read from
        /// the appmanifest files of the library the folder is in. Null when the folder is not in a Steam library.
        /// </summary>
        public static string AppIdFor(string gameDir)
        {
            try
            {
                string dir = gameDir.TrimEnd('\\', '/');
                string folder = Path.GetFileName(dir);
                string apps = Path.GetDirectoryName(Path.GetDirectoryName(dir));
                if (apps == null || !Directory.Exists(apps)) return null;
                foreach (string manifest in Directory.GetFiles(apps, "appmanifest_*.acf"))
                {
                    if (!string.Equals(ParseInstallDir(File.ReadAllText(manifest)), folder, StringComparison.OrdinalIgnoreCase)) continue;
                    string id = Path.GetFileNameWithoutExtension(manifest).Substring("appmanifest_".Length);
                    if (id.Length > 0 && id.All(char.IsDigit)) return id;
                }
            }
            catch (Exception)
            {
                // Not a Steam library, or unreadable: the caller starts the exe directly.
            }
            return null;
        }

        public static bool IsGameFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return false;
            try
            {
                return File.Exists(Path.Combine(folder, GameExe)) && Directory.Exists(Path.Combine(folder, "HometownStore_Data"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Searches the given Steam installation (and every library it lists) for the game.
        /// Returns null when not found.
        /// </summary>
        public static string FindGame(string steamPath)
        {
            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath)) return null;
            var libraries = new List<string> { steamPath };
            foreach (string vdfPath in new[] { Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"), Path.Combine(steamPath, "config", "libraryfolders.vdf") })
            {
                try
                {
                    if (File.Exists(vdfPath))
                    {
                        foreach (string lib in ParseLibraryFolders(File.ReadAllText(vdfPath))) AddUnique(libraries, lib);
                    }
                }
                catch (IOException)
                {
                    // Unreadable file: keep searching the others.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            foreach (string lib in libraries)
            {
                string apps = Path.Combine(lib, "steamapps");
                string folderName = GameFolderName;
                try
                {
                    string manifest = Path.Combine(apps, "appmanifest_" + AppId + ".acf");
                    if (File.Exists(manifest))
                    {
                        string dir = ParseInstallDir(File.ReadAllText(manifest));
                        if (!string.IsNullOrEmpty(dir)) folderName = dir;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                string candidate = Path.Combine(apps, "common", folderName);
                if (IsGameFolder(candidate)) return candidate;
            }
            return null;
        }
    }
}
