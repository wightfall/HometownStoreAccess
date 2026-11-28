using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using HometownStoreAccess.Core;

namespace HometownStoreAccess.Installer
{
    /// <summary>
    /// A deliberately simple window: every control has an accessible name, the tab order follows the
    /// visual order, results are shown in a message box (always announced by screen readers) and in a
    /// read-only status box you can tab to and review. The window opens on the status box (what was
    /// found, what is installed); when it updates an older mod, on the read-only What's new box instead.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private readonly Label _languageLabel = new Label();
        private readonly ComboBox _language = new ComboBox();
        private readonly Label _intro = new Label();
        private readonly Label _folderLabel = new Label();
        private readonly TextBox _folder = new TextBox();
        private readonly Button _browse = new Button();
        private readonly Label _statusLabel = new Label();
        private readonly TextBox _status = new TextBox();
        private readonly Button _install = new Button();
        private readonly Button _uninstall = new Button();
        private readonly CheckBox _launchAfter = new CheckBox();
        private readonly CheckBox _setGameLanguage = new CheckBox();
        private readonly CheckBox _removeBepInEx = new CheckBox();
        private readonly Button _checkUpdates = new Button();
        private readonly Button _openFolder = new Button();
        private readonly Button _close = new Button();
        private readonly Label _whatsNewLabel = new Label();
        private readonly TextBox _whatsNew = new TextBox();
        private readonly string _payloadVersion;
        private readonly string _notes;
        // The installed mod's version (from its DLL) and what the main button does, as last refreshed.
        private string _installedVersion;
        private InstallAction _action = InstallAction.Install;
        private readonly List<string> _languageCodes = new List<string>(Loc.SupportedLanguages);
        private bool _loading = true;

        public MainForm(string detectedFolder)
        {
            _payloadVersion = Program.PayloadVersion();
            _notes = Program.WhatsNew();
            bool notes = UpdateInfo.NoteLines(_notes).Count > 0;
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            // The release notes of this version go in a column of their own on the right.
            ClientSize = new Size(notes ? 1000 : 660, 560);

            Place(_languageLabel, 16, 14, 200, 24, 0);
            Place(_language, 220, 10, 424, 28, 1);
            _language.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string code in _languageCodes) _language.Items.Add(NativeName(code));
            _language.SelectedIndex = Math.Max(0, _languageCodes.IndexOf(InstallerPrefs.Language ?? Loc.Language));
            _language.SelectedIndexChanged += (s, e) =>
            {
                if (_loading) return;
                InstallerPrefs.Language = _languageCodes[_language.SelectedIndex];
                Loc.SetLanguage(InstallerPrefs.Language);
                ApplyTexts();
            };
            Loc.SetLanguage(_languageCodes[_language.SelectedIndex]);

            Place(_intro, 16, 46, 628, 64, 2);
            Place(_folderLabel, 16, 114, 628, 22, 3);
            Place(_folder, 16, 138, 516, 28, 4);
            _folder.Text = detectedFolder ?? string.Empty;
            _folder.TextChanged += (s, e) => RefreshStatus();
            Place(_browse, 540, 136, 104, 32, 5);
            _browse.Click += (s, e) => Browse();
            Place(_statusLabel, 16, 176, 628, 22, 6);
            Place(_status, 16, 200, 628, 110, 7);
            _status.Multiline = true;
            _status.ReadOnly = true;
            _status.ScrollBars = ScrollBars.Vertical;
            if (notes)
            {
                Place(_whatsNewLabel, 672, 14, 312, 22, 8);
                Place(_whatsNew, 672, 40, 312, 506, 9);
                _whatsNew.Multiline = true;
                _whatsNew.ReadOnly = true;
                _whatsNew.ScrollBars = ScrollBars.Vertical;
                _whatsNew.Text = string.Join(Environment.NewLine, UpdateInfo.NoteLines(_notes));
            }
            Place(_install, 16, 320, 210, 36, 10);
            _install.Click += (s, e) => DoInstall();
            Place(_uninstall, 234, 320, 210, 36, 11);
            _uninstall.Click += (s, e) => DoUninstall();
            Place(_launchAfter, 16, 364, 628, 28, 12);
            _launchAfter.Checked = InstallerPrefs.LaunchAfter;
            _launchAfter.CheckedChanged += (s, e) => InstallerPrefs.LaunchAfter = _launchAfter.Checked;
            Place(_setGameLanguage, 16, 394, 628, 28, 13);
            _setGameLanguage.Checked = InstallerPrefs.SetGameLanguage;
            _setGameLanguage.CheckedChanged += (s, e) => InstallerPrefs.SetGameLanguage = _setGameLanguage.Checked;
            Place(_removeBepInEx, 16, 424, 628, 28, 14);
            Place(_checkUpdates, 16, 462, 210, 36, 15);
            _checkUpdates.Click += async (s, e) => await CheckForUpdates(manual: true);
            Place(_openFolder, 234, 462, 210, 36, 16);
            _openFolder.Click += (s, e) => OpenGameFolder();
            Place(_close, 452, 510, 192, 36, 17);
            _close.Click += (s, e) => Close();

            AcceptButton = _install;
            CancelButton = _close;
            ApplyTexts();
            _loading = false;
            Shown += async (s, e) =>
            {
                // The read-only status says what was found (the game folder, the installed version). When
                // this installer updates an older mod, its What's new comes first instead.
                if (_action == InstallAction.Update && _whatsNew.Parent != null) _whatsNew.Focus();
                else _status.Focus();
                await CheckForUpdates(manual: false);
            };
        }

        private void Place(Control c, int x, int y, int w, int h, int tab)
        {
            c.Location = new Point(x, y);
            c.Size = new Size(w, h);
            c.TabIndex = tab;
            Controls.Add(c);
        }

        private static string NativeName(string code)
        {
            switch (code)
            {
                case "th": return "ไทย";
                case "ja": return "日本語";
                case "id": return "Bahasa Indonesia";
                case "zh": return "繁體中文";
                default: return "English";
            }
        }

        /// <summary>Sets every visible and accessible text in the chosen language.</summary>
        private void ApplyTexts()
        {
            Text = Loc.Get("inst_title") + (_payloadVersion != null ? " " + _payloadVersion : string.Empty);
            AccessibleName = Text;
            _languageLabel.Text = Loc.Get("inst_language");
            _language.AccessibleName = Loc.Get("inst_language");
            _intro.Text = Loc.Get("inst_intro");
            _folderLabel.Text = Loc.Get("inst_game_folder");
            _folder.AccessibleName = Loc.Get("inst_game_folder");
            _browse.Text = Loc.Get("inst_browse");
            _statusLabel.Text = Loc.Get("inst_status");
            _status.AccessibleName = Loc.Get("inst_status");
            _whatsNewLabel.Text = Loc.Get("inst_whats_new_in", _payloadVersion ?? "?");
            _whatsNew.AccessibleName = _whatsNewLabel.Text;
            _uninstall.Text = Loc.Get("inst_uninstall");
            _launchAfter.Text = Loc.Get("inst_launch_after");
            _setGameLanguage.Text = Loc.Get("inst_set_game_language");
            _removeBepInEx.Text = Loc.Get("inst_remove_bepinex");
            _checkUpdates.Text = Loc.Get("inst_check_updates");
            _openFolder.Text = Loc.Get("inst_open_folder");
            _close.Text = Loc.Get("inst_close");
            RefreshStatus();
        }

        private string GameFolder => _folder.Text.Trim().Trim('"');

        private void RefreshStatus()
        {
            string folder = GameFolder;
            // (Nothing from a folder typed before this one.)
            _installedVersion = null;
            _action = InstallAction.Install;
            if (!SteamLocator.IsGameFolder(folder))
            {
                _status.Text = string.IsNullOrEmpty(folder) ? Loc.Get("inst_not_found") : Loc.Get("inst_invalid_folder");
                _install.Text = Loc.Get("inst_install");
                _install.Enabled = false;
                _uninstall.Enabled = false;
                _removeBepInEx.Enabled = false;
                _openFolder.Enabled = false;
                return;
            }
            InstallStatus st = ModInstaller.GetStatus(folder);
            bool installed = st.State == InstallState.Installed;
            _installedVersion = installed ? st.InstalledVersion : null;
            _action = UpdateInfo.ActionFor(installed, _installedVersion, _payloadVersion);
            string text;
            switch (st.State)
            {
                case InstallState.Installed:
                    text = Loc.Get("inst_status_installed", st.InstalledVersion ?? "?");
                    break;
                case InstallState.BepInExOnly:
                    text = Loc.Get("inst_status_bepinex_only");
                    break;
                default:
                    text = Loc.Get("inst_status_none");
                    break;
            }
            // What this installer would do, with its own version.
            string payload = _payloadVersion ?? "?";
            switch (_action)
            {
                case InstallAction.Update:
                    _install.Text = Loc.Get("inst_update");
                    text += Environment.NewLine + Loc.Get("inst_status_will_update", payload);
                    break;
                case InstallAction.Reinstall:
                    _install.Text = Loc.Get("inst_reinstall");
                    text += Environment.NewLine + Loc.Get("inst_status_same", payload);
                    break;
                case InstallAction.InstallOlder:
                    _install.Text = Loc.Get("inst_install_older");
                    text += Environment.NewLine + Loc.Get("inst_status_older", payload);
                    break;
                default:
                    _install.Text = Loc.Get("inst_install");
                    text += Environment.NewLine + Loc.Get("inst_status_will_install", payload);
                    break;
            }
            if (st.OtherPluginsPresent) text += Environment.NewLine + Loc.Get("inst_other_plugins");
            text += Environment.NewLine + Loc.Get("inst_folder_is", folder);
            _status.Text = text;
            _install.Enabled = true;
            _uninstall.Enabled = st.State == InstallState.Installed;
            _removeBepInEx.Enabled = st.State != InstallState.NotInstalled && !st.OtherPluginsPresent;
            _removeBepInEx.Checked = st.BepInExInstalledByMod && !st.OtherPluginsPresent;
            _openFolder.Enabled = true;
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog { Description = Loc.Get("inst_choose_folder"), ShowNewFolderButton = false })
            {
                if (Directory.Exists(GameFolder)) dlg.SelectedPath = GameFolder;
                if (dlg.ShowDialog(this) == DialogResult.OK) _folder.Text = dlg.SelectedPath;
            }
            _folder.Focus();
        }

        private bool CheckGameClosed()
        {
            while (ModInstaller.IsGameRunning())
            {
                if (MessageBox.Show(this, Loc.Get("inst_game_running"), Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry) return false;
            }
            return true;
        }

        private void DoInstall()
        {
            if (_action == InstallAction.InstallOlder
                && MessageBox.Show(this, Loc.Get("inst_confirm_older", _payloadVersion ?? "?", _installedVersion ?? "?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                _install.Focus();
                return;
            }
            if (!CheckGameClosed()) return;
            try
            {
                using (Stream zip = Program.OpenPayload())
                {
                    if (zip == null)
                    {
                        MessageBox.Show(this, Loc.Get("inst_no_payload"), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    UseWaitCursor = true;
                    InstallResult r = ModInstaller.Install(GameFolder, zip);
                    UseWaitCursor = false;
                    if (r.Success && _setGameLanguage.Checked) WriteLanguageChoice(GameFolder, Loc.Language);
                    RefreshStatus();
                    MessageBox.Show(this, Describe(r, install: true), Text, MessageBoxButtons.OK, r.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                    if (r.Success && _launchAfter.Checked) LaunchGame(GameFolder);
                }
            }
            catch (UnauthorizedAccessException e) when (!IsElevated())
            {
                UseWaitCursor = false;
                OfferElevation(install: true, e);
            }
            catch (Exception e)
            {
                UseWaitCursor = false;
                MessageBox.Show(this, Loc.Get("inst_error", e.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _install.Focus();
        }

        private static bool IsElevated()
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>Quotes a path for a command line (a trailing backslash would escape the closing quote).</summary>
        public static string Quote(string path) => "\"" + (path ?? string.Empty).TrimEnd('\\') + "\"";

        /// <summary>
        /// The folder is protected: offers to run the same action again with administrator rights. The
        /// elevated copy runs without a window and shows its own result; the status here refreshes after it.
        /// </summary>
        private void OfferElevation(bool install, Exception cause)
        {
            if (MessageBox.Show(this, Loc.Get("inst_needs_admin") + Environment.NewLine + cause.Message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var args = new List<string> { install ? "/install" : "/uninstall", "/lang:" + Loc.Language };
            if (install && _launchAfter.Checked) args.Add("/launch");
            if (install && _setGameLanguage.Checked) args.Add("/gamelang");
            if (!install && _removeBepInEx.Checked) args.Add("/removebepinex");
            args.Add(Quote(GameFolder));
            try
            {
                Process p = Process.Start(new ProcessStartInfo(Application.ExecutablePath, string.Join(" ", args)) { UseShellExecute = true, Verb = "runas" });
                if (p == null) return;
                p.EnableRaisingEvents = true;
                p.Exited += (s, e) => BeginInvoke(new Action(RefreshStatus));
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // The Windows permission prompt was cancelled.
            }
        }

        /// <summary>Makes the game and the mod start in the installer's language.</summary>
        public static void WriteLanguageChoice(string gameDir, string language)
        {
            string locale = language == "zh" ? "zh-HK" : language;
            string cfg = ConfigEditor.ModConfigPath(gameDir);
            ConfigEditor.SetValue(cfg, "Speech", "GameLocale", locale);
            ConfigEditor.SetValue(cfg, "Speech", "ModLanguage", "Auto");
        }

        /// <summary>
        /// Starts the game through Steam when the folder is in a Steam library. Otherwise the exe is started
        /// through Explorer, so the game does not inherit the installer's administrator rights.
        /// </summary>
        public static void LaunchGame(string gameDir)
        {
            string appId = SteamLocator.AppIdFor(gameDir);
            try
            {
                if (appId != null)
                {
                    Process.Start(new ProcessStartInfo("steam://rungameid/" + appId) { UseShellExecute = true });
                    return;
                }
            }
            catch (Exception)
            {
                // Steam is not available: start the exe instead.
            }
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Path.Combine(gameDir, SteamLocator.GameExe) + "\"") { UseShellExecute = true });
        }

        private void DoUninstall()
        {
            if (!CheckGameClosed()) return;
            if (MessageBox.Show(this, Loc.Get("inst_confirm_uninstall"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                InstallResult r = ModInstaller.Uninstall(GameFolder, _removeBepInEx.Checked);
                RefreshStatus();
                MessageBox.Show(this, Describe(r, install: false), Text, MessageBoxButtons.OK, r.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException e) when (!IsElevated())
            {
                OfferElevation(install: false, e);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, Loc.Get("inst_error", e.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _install.Focus();
        }

        private void OpenGameFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + GameFolder + "\"") { UseShellExecute = true });
            }
            catch (Exception e)
            {
                MessageBox.Show(this, Loc.Get("inst_error", e.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Asks GitHub for the latest release; offers it when it is newer than both this installer and the
        /// mod that is installed. "You have" is the newer of the installed mod (its DLL) and this installer.
        /// </summary>
        private async Task CheckForUpdates(bool manual)
        {
            ReleaseInfo latest;
            try
            {
                latest = await Task.Run(() => FetchLatest());
            }
            catch (Exception e)
            {
                if (manual) MessageBox.Show(this, Loc.Get("inst_update_failed", e.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string current = HaveVersion();
            if (latest == null || !UpdateInfo.ShouldOffer(latest.Version, _installedVersion, _payloadVersion))
            {
                if (manual) MessageBox.Show(this, Loc.Get("inst_up_to_date", current ?? "?"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new UpdateForm(latest, current))
            {
                if (dlg.ShowDialog(this) != DialogResult.Yes) return;
            }
            await DownloadAndRun(latest);
        }

        /// <summary>The newer of the installed mod's version and this installer's (either may be missing).</summary>
        private string HaveVersion()
        {
            if (_installedVersion == null) return _payloadVersion;
            if (_payloadVersion == null) return _installedVersion;
            return UpdateInfo.Compare(_payloadVersion, _installedVersion) > 0 ? _payloadVersion : _installedVersion;
        }

        private static ReleaseInfo FetchLatest()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HometownStoreAccessInstaller");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                string json = http.GetStringAsync(UpdateInfo.LatestApi).GetAwaiter().GetResult();
                return UpdateInfo.Parse(json);
            }
        }

        private async Task DownloadAndRun(ReleaseInfo latest)
        {
            if (string.IsNullOrEmpty(latest.InstallerUrl))
            {
                if (!string.IsNullOrEmpty(latest.PageUrl)) Process.Start(new ProcessStartInfo(latest.PageUrl) { UseShellExecute = true });
                return;
            }
            try
            {
                UseWaitCursor = true;
                _status.Text = Loc.Get("inst_downloading", latest.Version);
                string target = Path.Combine(Path.GetTempPath(), "HometownStoreAccessInstaller-" + latest.Version + ".exe");
                await Task.Run(() =>
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                    {
                        http.DefaultRequestHeaders.UserAgent.ParseAdd("HometownStoreAccessInstaller");
                        byte[] data = http.GetByteArrayAsync(latest.InstallerUrl).GetAwaiter().GetResult();
                        File.WriteAllBytes(target, data);
                    }
                });
                UseWaitCursor = false;
                Process.Start(new ProcessStartInfo(target, Quote(GameFolder)) { UseShellExecute = true });
                Close();
            }
            catch (Exception e)
            {
                UseWaitCursor = false;
                RefreshStatus();
                MessageBox.Show(this, Loc.Get("inst_update_failed", e.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static string Describe(InstallResult r, bool install)
        {
            if (install)
            {
                if (!r.Success) return Loc.Get("inst_failed");
                return r.InstalledBepInEx ? Loc.Get("inst_done_install_bepinex") : Loc.Get("inst_done_install");
            }
            if (!r.Success) return Loc.Get(r.Messages.Contains("files_locked") ? "inst_uninstall_locked" : "inst_failed");
            string msg = Loc.Get("inst_done_uninstall");
            if (r.Messages.Contains("bepinex_kept_other_plugins")) msg += " " + Loc.Get("inst_bepinex_kept");
            else if (r.Messages.Contains("bepinex_removed")) msg += " " + Loc.Get("inst_bepinex_removed");
            if (r.Messages.Contains("translations_kept")) msg += " " + Loc.Get("inst_translations_kept");
            return msg;
        }
    }

    /// <summary>"A new version is available" with the release notes, one line per row, and Yes / No.</summary>
    internal sealed class UpdateForm : Form
    {
        public UpdateForm(ReleaseInfo latest, string current)
        {
            Text = Loc.Get("inst_update_title");
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(600, 420);

            var question = new Label { Text = Loc.Get("inst_update_question", latest.Version, current ?? "?"), Location = new Point(16, 12), Size = new Size(568, 48), TabIndex = 0 };
            var notesLabel = new Label { Text = Loc.Get("inst_whats_new"), Location = new Point(16, 64), Size = new Size(568, 22), TabIndex = 1 };
            var notes = new TextBox
            {
                Location = new Point(16, 88),
                Size = new Size(568, 264),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                TabIndex = 2,
                AccessibleName = Loc.Get("inst_whats_new"),
                Text = string.Join(Environment.NewLine, UpdateInfo.NoteLines(latest.Notes).DefaultIfEmpty(Loc.Get("inst_no_notes"))),
            };
            var yes = new Button { Text = Loc.Get("inst_yes"), Location = new Point(296, 364), Size = new Size(140, 36), TabIndex = 3, DialogResult = DialogResult.Yes };
            var no = new Button { Text = Loc.Get("inst_no"), Location = new Point(444, 364), Size = new Size(140, 36), TabIndex = 4, DialogResult = DialogResult.No };
            Controls.AddRange(new Control[] { question, notesLabel, notes, yes, no });
            AcceptButton = yes;
            CancelButton = no;
            Shown += (s, e) => notes.Focus();
        }
    }

    /// <summary>Remembers the installer's own choices between runs (%APPDATA%\HometownStoreAccess\installer.txt).</summary>
    internal static class InstallerPrefs
    {
        private static readonly string File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HometownStoreAccess", "installer.txt");
        private static Dictionary<string, string> _values;

        private static Dictionary<string, string> Values
        {
            get
            {
                if (_values != null) return _values;
                _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (System.IO.File.Exists(File))
                    {
                        foreach (string line in System.IO.File.ReadAllLines(File))
                        {
                            int eq = line.IndexOf('=');
                            if (eq > 0) _values[line.Substring(0, eq)] = line.Substring(eq + 1);
                        }
                    }
                }
                catch (Exception)
                {
                    // Preferences are optional.
                }
                return _values;
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(File));
                System.IO.File.WriteAllLines(File, Values.Select(kv => kv.Key + "=" + kv.Value).ToArray());
            }
            catch (Exception)
            {
                // Preferences are optional.
            }
        }

        public static string Language
        {
            get => Values.TryGetValue("language", out string v) ? v : null;
            set { Values["language"] = value; Save(); }
        }

        public static bool LaunchAfter
        {
            get => !Values.TryGetValue("launch", out string v) || v == "1";
            set { Values["launch"] = value ? "1" : "0"; Save(); }
        }

        public static bool SetGameLanguage
        {
            get => Values.TryGetValue("gamelang", out string v) && v == "1";
            set { Values["gamelang"] = value ? "1" : "0"; Save(); }
        }
    }
}
