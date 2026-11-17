using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace HometownStoreAccess.Installer
{
    public enum InstallState
    {
        NotInstalled,
        BepInExOnly,
        Installed,
    }

    public sealed class InstallStatus
    {
        public InstallState State;
        public string InstalledVersion;
        public bool BepInExInstalledByMod;
        public bool OtherPluginsPresent;
    }

    public sealed class InstallResult
    {
        public bool Success;
        public readonly List<string> Messages = new List<string>();
        public int FilesWritten;
        public int FilesRemoved;
        public bool InstalledBepInEx;
    }

    /// <summary>
    /// Installs, updates and removes BepInEx and the mod using the release zip, whose paths are
    /// relative to the game folder. A small record file remembers what the installer added so that
    /// uninstalling never removes another mod's files.
    /// </summary>
    public static class ModInstaller
    {
        public const string PluginFolder = "BepInEx/plugins/HometownStoreAccess";
        public const string RecordFile = "install-record.txt";
        public const string VersionFile = "version.txt";
        /// <summary>Translation files (the player's own and the generated templates) live here, inside the plugin folder.</summary>
        public const string LangFolder = "lang";
        public const string GameProcessName = "HometownStore";

        private static readonly string[] BepInExRootFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt" };

        public static bool IsGameRunning()
        {
            try
            {
                return Process.GetProcessesByName(GameProcessName).Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string Normalize(string zipPath) => zipPath.Replace('\\', '/').TrimStart('/');

        public static bool IsModEntry(string zipPath)
        {
            string p = Normalize(zipPath);
            return p.StartsWith(PluginFolder + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static InstallStatus GetStatus(string gameDir)
        {
            var status = new InstallStatus();
            string pluginDir = Path.Combine(gameDir, PluginFolder.Replace('/', Path.DirectorySeparatorChar));
            bool bepinex = File.Exists(Path.Combine(gameDir, "BepInEx", "core", "BepInEx.dll")) && File.Exists(Path.Combine(gameDir, "winhttp.dll"));
            bool mod = File.Exists(Path.Combine(pluginDir, "HometownStoreAccess.dll"));
            status.State = mod && bepinex ? InstallState.Installed : bepinex ? InstallState.BepInExOnly : InstallState.NotInstalled;
            Dictionary<string, string> record = ReadRecord(pluginDir);
            if (record.TryGetValue("version", out string v)) status.InstalledVersion = v;
            else if (File.Exists(Path.Combine(pluginDir, VersionFile))) status.InstalledVersion = File.ReadAllText(Path.Combine(pluginDir, VersionFile)).Trim();
            status.BepInExInstalledByMod = record.TryGetValue("bepinex_by_installer", out string b) && b == "true";
            status.OtherPluginsPresent = OtherPlugins(gameDir).Any();
            return status;
        }

        /// <summary>Plugin files or folders under BepInEx/plugins that do not belong to this mod.</summary>
        public static IEnumerable<string> OtherPlugins(string gameDir)
        {
            string plugins = Path.Combine(gameDir, "BepInEx", "plugins");
            if (!Directory.Exists(plugins)) yield break;
            foreach (string entry in Directory.EnumerateFileSystemEntries(plugins))
            {
                if (!string.Equals(Path.GetFileName(entry), "HometownStoreAccess", StringComparison.OrdinalIgnoreCase)) yield return entry;
            }
        }

        /// <summary>
        /// Installs or updates. BepInEx is only written when missing (or when <paramref name="repairBepInEx"/> is set),
        /// so an existing BepInEx setup with other mods is left alone.
        /// </summary>
        public static InstallResult Install(string gameDir, Stream releaseZip, bool repairBepInEx = false)
        {
            var result = new InstallResult();
            if (!SteamLocator.IsGameFolder(gameDir))
            {
                result.Messages.Add("not_game_folder");
                return result;
            }
            string pluginDir = Path.Combine(gameDir, PluginFolder.Replace('/', Path.DirectorySeparatorChar));
            InstallStatus before = GetStatus(gameDir);
            bool writeBepInEx = repairBepInEx || before.State == InstallState.NotInstalled;
            var modFiles = new List<string>();
            string version = null;

            using (var zip = new ZipArchive(releaseZip, ZipArchiveMode.Read, leaveOpen: true))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string rel = Normalize(entry.FullName);
                    if (rel.Length == 0 || rel.EndsWith("/", StringComparison.Ordinal)) continue;
                    if (rel.Contains("../") || Path.IsPathRooted(rel)) throw new InvalidDataException("Unsafe path in release archive: " + rel);
                    bool isMod = IsModEntry(rel);
                    if (!isMod && !writeBepInEx) continue;
                    string target = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (Stream src = entry.Open())
                    using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        src.CopyTo(dst);
                    }
                    result.FilesWritten++;
                    if (isMod)
                    {
                        string inPlugin = rel.Substring(PluginFolder.Length + 1);
                        modFiles.Add(inPlugin);
                        if (string.Equals(inPlugin, VersionFile, StringComparison.OrdinalIgnoreCase))
                        {
                            version = File.ReadAllText(target).Trim();
                        }
                    }
                }
            }

            // Remove files left over from an older version of the mod.
            if (Directory.Exists(pluginDir))
            {
                var keep = new HashSet<string>(modFiles.Select(f => f.Replace('/', Path.DirectorySeparatorChar)), StringComparer.OrdinalIgnoreCase) { RecordFile };
                foreach (string file in Directory.GetFiles(pluginDir, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(pluginDir.Length).TrimStart(Path.DirectorySeparatorChar);
                    if (keep.Contains(rel) || IsTranslationFile(rel)) continue;
                    try
                    {
                        File.Delete(file);
                        result.FilesRemoved++;
                    }
                    catch (IOException)
                    {
                        // A locked leftover is harmless.
                    }
                }
            }

            result.InstalledBepInEx = writeBepInEx;
            bool bepinexByInstaller = writeBepInEx ? before.State == InstallState.NotInstalled || before.BepInExInstalledByMod : before.BepInExInstalledByMod;
            WriteRecord(pluginDir, new Dictionary<string, string>
            {
                ["version"] = version ?? "unknown",
                ["bepinex_by_installer"] = bepinexByInstaller ? "true" : "false",
                ["installed_utc"] = DateTime.UtcNow.ToString("u"),
            });
            result.Success = File.Exists(Path.Combine(pluginDir, "HometownStoreAccess.dll"));
            result.Messages.Add(result.Success ? "installed" : "install_incomplete");
            return result;
        }

        /// <summary>
        /// Removes the mod. BepInEx is removed too when requested and no other plugins depend on it.
        /// While BepInEx stays, the mod's settings (BepInEx/config) and the player's own translation files
        /// (the plugin's lang folder) are kept, so a reinstall picks them up again.
        /// </summary>
        public static InstallResult Uninstall(string gameDir, bool removeBepInEx)
        {
            var result = new InstallResult();
            string pluginDir = Path.Combine(gameDir, PluginFolder.Replace('/', Path.DirectorySeparatorChar));
            bool otherPlugins = OtherPlugins(gameDir).Any();
            bool removingBepInEx = removeBepInEx && !otherPlugins;
            bool keptTranslations = false;
            if (Directory.Exists(pluginDir))
            {
                foreach (string file in Directory.GetFiles(pluginDir, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(pluginDir.Length).TrimStart(Path.DirectorySeparatorChar);
                    if (!removingBepInEx && IsUserTranslation(rel))
                    {
                        keptTranslations = true;
                        continue;
                    }
                    File.Delete(file);
                    result.FilesRemoved++;
                }
                RemoveEmptyFolders(pluginDir);
            }
            if (keptTranslations) result.Messages.Add("translations_kept");
            if (removeBepInEx && otherPlugins)
            {
                result.Messages.Add("bepinex_kept_other_plugins");
            }
            else if (removingBepInEx)
            {
                foreach (string f in BepInExRootFiles)
                {
                    string p = Path.Combine(gameDir, f);
                    if (File.Exists(p))
                    {
                        File.Delete(p);
                        result.FilesRemoved++;
                    }
                }
                string bep = Path.Combine(gameDir, "BepInEx");
                if (Directory.Exists(bep))
                {
                    result.FilesRemoved += Directory.GetFiles(bep, "*", SearchOption.AllDirectories).Length;
                    Directory.Delete(bep, recursive: true);
                }
                result.Messages.Add("bepinex_removed");
            }
            bool modGone = !File.Exists(Path.Combine(pluginDir, "HometownStoreAccess.dll"));
            result.Success = modGone && (keptTranslations || !Directory.Exists(pluginDir));
            result.Messages.Add(result.Success ? "uninstalled" : "uninstall_incomplete");
            return result;
        }

        /// <summary>Anything in the plugin's lang folder: translations the player made, and the generated templates.</summary>
        public static bool IsTranslationFile(string relInPlugin)
        {
            string p = relInPlugin.Replace('\\', '/');
            return p.StartsWith(LangFolder + "/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A translation file the player made: lang/&lt;code&gt;.mod.txt or lang/&lt;code&gt;.game.txt.
        /// The generated templates and anything shipped with the mod are not kept on uninstall.
        /// </summary>
        public static bool IsUserTranslation(string relInPlugin)
        {
            if (!IsTranslationFile(relInPlugin)) return false;
            string name = Path.GetFileName(relInPlugin);
            if (name.StartsWith("template.", StringComparison.OrdinalIgnoreCase)) return false;
            return name.EndsWith(".mod.txt", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".game.txt", StringComparison.OrdinalIgnoreCase);
        }

        private static void RemoveEmptyFolders(string dir)
        {
            foreach (string sub in Directory.GetDirectories(dir)) RemoveEmptyFolders(sub);
            if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }

        internal static Dictionary<string, string> ReadRecord(string pluginDir)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(pluginDir, RecordFile);
            if (!File.Exists(path)) return dict;
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                dict[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return dict;
        }

        private static void WriteRecord(string pluginDir, Dictionary<string, string> values)
        {
            Directory.CreateDirectory(pluginDir);
            var sb = new StringBuilder();
            sb.AppendLine("# Written by the Hometown Store Access installer. Used to update and uninstall cleanly.");
            foreach (var kv in values) sb.Append(kv.Key).Append('=').AppendLine(kv.Value);
            File.WriteAllText(Path.Combine(pluginDir, RecordFile), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
