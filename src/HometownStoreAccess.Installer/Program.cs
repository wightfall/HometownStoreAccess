using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HometownStoreAccess.Core;
using Microsoft.Win32;

namespace HometownStoreAccess.Installer
{
    internal static class Program
    {
        /// <summary>
        /// Usage: HometownStoreAccessInstaller.exe [/install | /uninstall] [/removebepinex] [/launch] [/gamelang] [/quiet] [/lang:th] ["game folder"]
        /// Without /install or /uninstall the window opens. Exit code 0 means success.
        /// /launch starts the game after installing; /gamelang makes the game and the mod start in the /lang language.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            string lang = args.FirstOrDefault(a => a.StartsWith("/lang:", StringComparison.OrdinalIgnoreCase))?.Substring(6);
            // An explicit /lang: wins and is remembered; otherwise the language chosen last time, then Windows' language.
            if (!string.IsNullOrEmpty(lang)) InstallerPrefs.Language = Loc.NormalizeLanguage(lang);
            Loc.SetLanguage(InstallerPrefs.Language ?? CultureInfo.CurrentUICulture.Name);

            bool install = args.Any(a => a.Equals("/install", StringComparison.OrdinalIgnoreCase));
            bool uninstall = args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
            bool removeBepInEx = args.Any(a => a.Equals("/removebepinex", StringComparison.OrdinalIgnoreCase));
            bool quiet = args.Any(a => a.Equals("/quiet", StringComparison.OrdinalIgnoreCase));
            bool launch = args.Any(a => a.Equals("/launch", StringComparison.OrdinalIgnoreCase));
            bool gameLanguage = args.Any(a => a.Equals("/gamelang", StringComparison.OrdinalIgnoreCase));
            string folder = args.FirstOrDefault(a => !a.StartsWith("/", StringComparison.Ordinal))?.Trim().Trim('"') ?? DetectGameFolder();

            if (install || uninstall)
            {
                return RunCommandLine(install, folder, removeBepInEx, quiet, launch, gameLanguage);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(folder));
            return 0;
        }

        private static int RunCommandLine(bool install, string folder, bool removeBepInEx, bool quiet, bool launch, bool gameLanguage)
        {
            try
            {
                if (!SteamLocator.IsGameFolder(folder))
                {
                    Report(Loc.Get("inst_invalid_folder"), quiet, error: true);
                    return 2;
                }
                if (ModInstaller.IsGameRunning())
                {
                    Report(Loc.Get("inst_game_running"), quiet, error: true);
                    return 3;
                }
                InstallResult r;
                if (install)
                {
                    using (Stream zip = OpenPayload())
                    {
                        if (zip == null)
                        {
                            Report(Loc.Get("inst_no_payload"), quiet, error: true);
                            return 4;
                        }
                        r = ModInstaller.Install(folder, zip);
                    }
                }
                else
                {
                    r = ModInstaller.Uninstall(folder, removeBepInEx);
                }
                if (install && r.Success && gameLanguage) MainForm.WriteLanguageChoice(folder, Loc.Language);
                Report(MainForm.Describe(r, install), quiet, error: !r.Success);
                if (install && r.Success && launch) MainForm.LaunchGame(folder);
                return r.Success ? 0 : 1;
            }
            catch (Exception e)
            {
                Report(Loc.Get("inst_error", e.Message), quiet, error: true);
                return 5;
            }
        }

        private static void Report(string message, bool quiet, bool error)
        {
            Console.WriteLine(message);
            if (!quiet) MessageBox.Show(message, Loc.Get("inst_title"), MessageBoxButtons.OK, error ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        }

        /// <summary>The embedded release zip, or a HometownStoreAccess-*.zip next to the installer.</summary>
        public static Stream OpenPayload()
        {
            Stream embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("release.zip");
            if (embedded != null)
            {
                var copy = new MemoryStream();
                embedded.CopyTo(copy);
                embedded.Dispose();
                copy.Position = 0;
                return copy;
            }
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string zip = Directory.GetFiles(dir, "HometownStoreAccess-*.zip").OrderByDescending(f => f).FirstOrDefault();
            return zip != null ? File.OpenRead(zip) : null;
        }

        public static string PayloadVersion()
        {
            using (Stream s = OpenPayload())
            {
                if (s == null) return null;
                using (var zip = new System.IO.Compression.ZipArchive(s))
                {
                    var entry = zip.GetEntry(ModInstaller.PluginFolder + "/" + ModInstaller.VersionFile);
                    if (entry == null) return null;
                    using (var r = new StreamReader(entry.Open())) return r.ReadToEnd().Trim();
                }
            }
        }

        public static string DetectGameFolder()
        {
            foreach (string steam in SteamCandidates())
            {
                string found = SteamLocator.FindGame(steam);
                if (found != null) return found;
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<string> SteamCandidates()
        {
            string Read(RegistryKey root, string path, string name)
            {
                try
                {
                    using (RegistryKey k = root.OpenSubKey(path)) return k?.GetValue(name) as string;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            string user = Read(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            if (!string.IsNullOrEmpty(user)) yield return user.Replace('/', '\\');
            string machine = Read(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") ?? Read(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
            if (!string.IsNullOrEmpty(machine)) yield return machine;
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");
        }
    }
}
