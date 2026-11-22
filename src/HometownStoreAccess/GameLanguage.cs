using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace HometownStoreAccess
{
    /// <summary>
    /// Translation files in BepInEx/plugins/HometownStoreAccess/lang:
    ///   &lt;code&gt;.mod.txt   the mod's own messages (keys from template.mod.txt)
    ///   &lt;code&gt;.game.txt  the game's own text, "Table/Key = text" (keys from template.game.txt)
    /// Any language code works, also ones the game does not ship; the game then shows its @base language
    /// (English by default) with every translated entry replaced. Files are reloaded while the game runs.
    /// </summary>
    internal static class LanguageFiles
    {
        private static readonly Dictionary<string, LangFile> GameFiles = new Dictionary<string, LangFile>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> Stamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<(StringTable table, string key, string original, bool added)> Applied = new List<(StringTable, string, string, bool)>();
        // Entries of the active translation whose table the game does not ship for the selected locale
        // (the demo has no Japanese or Chinese MainMenu table, for example). GameTextFallbackPatch serves them.
        private static readonly Dictionary<string, string> Unplaced = new Dictionary<string, string>(StringComparer.Ordinal);

        public static bool TryGetUnplaced(string tableName, string key, out string text)
        {
            // Asked for every text the game looks up: no string building while there is nothing to find.
            if (Unplaced.Count == 0)
            {
                text = null;
                return false;
            }
            return Unplaced.TryGetValue(tableName + "/" + key, out text);
        }
        private static float _nextPoll;
        // After start-up the folder is read on a worker thread, so the file system never holds up a frame:
        // the worker reads the file times into a new dictionary and hands it over only when they changed.
        private static volatile bool _scanRunning;
        private static Dictionary<string, DateTime> _scanResult;
        private static readonly object ScanLock = new object();
        private static volatile string _scanError;
        private static bool _templatesWritten;
        private static bool _gameTextDirty = true;

        public static string Folder => Path.Combine(Plugin.PluginDirectory, "lang");

        /// <summary>Game text translations shipped with the mod (the player's own files in lang go on top).</summary>
        public static string BuiltInFolder => Path.Combine(Plugin.PluginDirectory, "translations");

        private static readonly Dictionary<string, LangFile> BuiltIn = new Dictionary<string, LangFile>(StringComparer.OrdinalIgnoreCase);
        private static bool _builtInLoaded;

        private static void LoadBuiltIn()
        {
            _builtInLoaded = true;
            BuiltIn.Clear();
            if (!Directory.Exists(BuiltInFolder)) return;
            foreach (string file in Directory.GetFiles(BuiltInFolder, "*.game.txt"))
            {
                string name = Path.GetFileName(file);
                string code = LangFile.GameTextLanguage(name.Substring(0, name.Length - ".game.txt".Length));
                LangFile f = LangFile.Load(file);
                foreach (string err in f.Errors) Log.Warn("translations/" + name + ": " + err);
                BuiltIn[code] = BuiltIn.TryGetValue(code, out LangFile earlier) ? LangFile.Merge(earlier, f) : f;
                Log.Info("Loaded built-in game translation " + name + " (" + f.Entries.Count + " entries)");
            }
        }

        public static string DisplayName(string code)
        {
            string builtIn = LanguageNames.Spoken(code);
            if (builtIn != null) return builtIn;
            string n = Loc.ExternalName(code);
            if (!string.IsNullOrEmpty(n)) return n;
            if (GameFiles.TryGetValue(code ?? string.Empty, out LangFile g) && !string.IsNullOrEmpty(g.Name)) return g.Name;
            return code;
        }

        public static bool HasGameText(string code) => code != null && GameFiles.ContainsKey(code);

        public static string BaseLocaleOf(string code) => GameFiles.TryGetValue(code, out LangFile f) ? f.BaseLocale : "en";

        /// <summary>Languages that exist only through translation files.</summary>
        public static IEnumerable<string> ExtraLanguages()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in Loc.AvailableLanguages) if (Loc.IsExternalOnly(l)) set.Add(l);
            foreach (string l in GameFiles.Keys) if (!Loc.SupportedLanguages.Contains(l, StringComparer.OrdinalIgnoreCase)) set.Add(l);
            return set.OrderBy(x => x);
        }

        public static void Update()
        {
            // The files are checked every 2 seconds; but text still waiting for the game's localization
            // goes in the frame it is ready, so the first screen is already in the player's language
            // (for Thai and English, which need none of it there, only after that screen has been read).
            bool deferred = GameLanguage.StartupWorkDeferred;
            bool waiting = _builtInLoaded && (!_templatesWritten || _gameTextDirty) && !deferred;
            // Start-up (and re-applying game text) reads the folder at once; afterwards a worker does.
            bool startup = !_builtInLoaded || !_templatesWritten || _gameTextDirty;
            bool found = !startup && TakeScanResult();
            if (!found && Time.unscaledTime < _nextPoll && !(waiting && LocalizationReady())) return;
            _nextPoll = Time.unscaledTime + (startup ? 2f : 1f);
            try
            {
                bool changed;
                if (startup)
                {
                    // A worker's older result would only repeat what this scan finds.
                    lock (ScanLock) _scanResult = null;
                    Directory.CreateDirectory(Folder);
                    changed = ScanForChanges();
                }
                else
                {
                    changed = found;
                    string error = _scanError;
                    if (error != null)
                    {
                        _scanError = null;
                        Log.Warn("Language files: " + error);
                    }
                    StartBackgroundScan();
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                if (!_builtInLoaded)
                {
                    LoadBuiltIn();
                    changed = true;
                    Log.Debug("Built-in translations loaded in " + sw.ElapsedMilliseconds + " ms");
                }
                if (changed) Reload();
                if (!_templatesWritten && LocalizationReady() && !deferred)
                {
                    sw.Restart();
                    WriteTemplates();
                    Log.Debug("Translation templates checked in " + sw.ElapsedMilliseconds + " ms");
                }
                if (_gameTextDirty && LocalizationReady() && !deferred)
                {
                    sw.Restart();
                    ApplyGameText();
                    Log.Debug("Game text applied in " + sw.ElapsedMilliseconds + " ms");
                }
            }
            catch (Exception e)
            {
                Log.Warn("Language files: " + e.Message);
                _nextPoll = Time.unscaledTime + 10f;
            }
        }

        public static void MarkGameTextDirty() => _gameTextDirty = true;

        /// <summary>Game text from translation files is still waiting to go into the game's tables.</summary>
        public static bool GameTextPending => _gameTextDirty || !_builtInLoaded;

        private static bool LocalizationReady()
        {
            try { return LocalizationSettings.InitializationOperation.IsDone && LocalizationSettings.SelectedLocale != null; }
            catch { return false; }
        }

        /// <summary>The write time of every translation file in the folder (safe on any thread).</summary>
        private static Dictionary<string, DateTime> ReadStamps()
        {
            var now = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(Folder, "*.txt"))
            {
                if (IsTranslationFileName(Path.GetFileName(file))) now[file] = File.GetLastWriteTimeUtc(file);
            }
            return now;
        }

        private static bool SameStamps(Dictionary<string, DateTime> a, Dictionary<string, DateTime> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out DateTime t) || t != kv.Value) return false;
            }
            return true;
        }

        private static void UseStamps(Dictionary<string, DateTime> now)
        {
            Stamps.Clear();
            foreach (var kv in now) Stamps[kv.Key] = kv.Value;
        }

        /// <summary>Reads the folder now (start-up); true when a file was added, changed or removed.</summary>
        private static bool ScanForChanges()
        {
            Dictionary<string, DateTime> now = ReadStamps();
            if (SameStamps(now, Stamps)) return false;
            UseStamps(now);
            return true;
        }

        private static void StartBackgroundScan()
        {
            if (_scanRunning) return;
            _scanRunning = true;
            // A copy: the worker never touches the dictionary the main thread uses.
            var known = new Dictionary<string, DateTime>(Stamps, StringComparer.OrdinalIgnoreCase);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    Dictionary<string, DateTime> now = ReadStamps();
                    if (!SameStamps(now, known))
                    {
                        lock (ScanLock) _scanResult = now;
                    }
                }
                catch (Exception e)
                {
                    _scanError = e.Message;
                }
                finally
                {
                    _scanRunning = false;
                }
            });
        }

        /// <summary>True (once) when the worker found changed files; their times are then the known ones.</summary>
        private static bool TakeScanResult()
        {
            Dictionary<string, DateTime> now;
            lock (ScanLock)
            {
                now = _scanResult;
                _scanResult = null;
            }
            if (now == null) return false;
            UseStamps(now);
            return true;
        }

        private static void Reload()
        {
            Loc.ClearExternal();
            GameFiles.Clear();
            foreach (var kv in BuiltIn) GameFiles[kv.Key] = kv.Value;
            foreach (string file in Stamps.Keys)
            {
                string name = Path.GetFileName(file);
                LangFile f = LangFile.Load(file);
                foreach (string err in f.Errors) Log.Warn(name + ": " + err);
                if (name.EndsWith(".mod.txt", StringComparison.OrdinalIgnoreCase))
                {
                    string code = name.Substring(0, name.Length - ".mod.txt".Length);
                    Loc.SetExternal(code, f.Entries, f.Name);
                    Log.Info("Loaded mod translation " + name + " (" + f.Entries.Count + " entries)");
                }
                else if (name.EndsWith(".game.txt", StringComparison.OrdinalIgnoreCase))
                {
                    string code = LangFile.GameTextLanguage(name.Substring(0, name.Length - ".game.txt".Length));
                    // The player's file goes on top of the built-in translation of the same language.
                    GameFiles[code] = GameFiles.TryGetValue(code, out LangFile under) ? LangFile.Merge(under, f) : f;
                    Log.Info("Loaded game translation " + name + " (" + f.Entries.Count + " entries, base " + f.BaseLocale + ")");
                }
            }
            GameLanguage.SyncModLanguage();
            _gameTextDirty = true;
            if (Time.realtimeSinceStartup > 10f) SpeechService.Say(Loc.Get("lang_reloaded"), false);
        }

        /// <summary>A translation file the mod loads: &lt;code&gt;.mod.txt or &lt;code&gt;.game.txt (not the templates).</summary>
        public static bool IsTranslationFileName(string name)
        {
            if (name.StartsWith("template.", StringComparison.OrdinalIgnoreCase)) return false;
            return name.EndsWith(".mod.txt", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".game.txt", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes template.mod.txt and template.game.txt for translators, and rewrites them whenever the
        /// mod's or the game's English text has changed (after an update), so they always list every key.
        /// Runs before any game text is replaced, so the game template holds the original English.
        /// </summary>
        private static void WriteTemplates()
        {
            _templatesWritten = true;
            var modEntries = Loc.AllKeys.Select(k => new KeyValuePair<string, string>(k, Loc.EnglishText(k)));
            WriteIfChanged(Path.Combine(Folder, "template.mod.txt"), LangFile.BuildTemplate(TemplateHeader("mod"), modEntries));

            Locale en = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code.StartsWith("en"));
            var entries = new List<KeyValuePair<string, string>>();
            // A translation file based on English may be in the English tables already (the language can
            // change before this runs): what it replaced is taken from what the mod kept, and what it added
            // is left out.
            var replaced = new Dictionary<(StringTable, string), (string original, bool added)>();
            foreach (var a in Applied)
            {
                if (a.table != null && !replaced.ContainsKey((a.table, a.key))) replaced.Add((a.table, a.key), (a.original, a.added));
            }
            foreach (LocalizationTable t in Enum.GetValues(typeof(LocalizationTable)))
            {
                string tableName = t.GetTableName();
                StringTable table = en != null ? LocalizationSettings.StringDatabase.GetTable(tableName, en) : null;
                if (table == null) continue;
                foreach (StringTableEntry e in table.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
                {
                    if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                    string value = e.Value ?? string.Empty;
                    if (replaced.TryGetValue((table, e.Key), out var r))
                    {
                        if (r.added) continue;
                        value = r.original ?? string.Empty;
                    }
                    entries.Add(new KeyValuePair<string, string>(tableName + "/" + e.Key, value));
                }
            }
            if (entries.Count > 0) WriteIfChanged(Path.Combine(Folder, "template.game.txt"), LangFile.BuildTemplate(TemplateHeader("game"), entries));
        }

        /// <summary>The game's own non-empty entries in the given locale, for every English key (for diagnostics).</summary>
        public static List<KeyValuePair<string, string>> ExistingGameEntries(string localeCode)
        {
            var result = new List<KeyValuePair<string, string>>();
            Locale en = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code.StartsWith("en"));
            Locale target = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code.StartsWith(localeCode, StringComparison.OrdinalIgnoreCase));
            if (en == null || target == null) return result;
            foreach (LocalizationTable t in Enum.GetValues(typeof(LocalizationTable)))
            {
                string tableName = t.GetTableName();
                StringTable enTable = LocalizationSettings.StringDatabase.GetTable(tableName, en);
                StringTable table = LocalizationSettings.StringDatabase.GetTable(tableName, target);
                if (enTable == null || table == null) continue;
                foreach (StringTableEntry e in enTable.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
                {
                    if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                    StringTableEntry other = table.GetEntry(e.Key);
                    if (other != null && !string.IsNullOrWhiteSpace(other.Value)) result.Add(new KeyValuePair<string, string>(tableName + "/" + e.Key, other.Value));
                }
            }
            return result;
        }

        /// <summary>Game text entries that exist in English but are empty or absent in the given locale (for diagnostics).</summary>
        public static List<KeyValuePair<string, string>> MissingGameEntries(string localeCode)
        {
            var result = new List<KeyValuePair<string, string>>();
            Locale en = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code.StartsWith("en"));
            Locale target = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code.StartsWith(localeCode, StringComparison.OrdinalIgnoreCase));
            if (en == null || target == null) return result;
            foreach (LocalizationTable t in Enum.GetValues(typeof(LocalizationTable)))
            {
                string tableName = t.GetTableName();
                StringTable enTable = LocalizationSettings.StringDatabase.GetTable(tableName, en);
                StringTable table = LocalizationSettings.StringDatabase.GetTable(tableName, target);
                if (enTable == null) continue;
                foreach (StringTableEntry e in enTable.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
                {
                    if (e == null || string.IsNullOrEmpty(e.Key) || string.IsNullOrEmpty(e.Value)) continue;
                    StringTableEntry other = table?.GetEntry(e.Key);
                    if (other == null || string.IsNullOrWhiteSpace(other.Value)) result.Add(new KeyValuePair<string, string>(tableName + "/" + e.Key, e.Value));
                }
            }
            return result;
        }

        private static void WriteIfChanged(string path, string text)
        {
            if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == text) return;
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Log.Info("Wrote " + path);
        }

        private static string TemplateHeader(string kind)
        {
            return "Hometown Store Access translation template (" + kind + " text).\n" +
                   "Copy this file to <code>." + kind + ".txt, for example es." + kind + ".txt, and fill in the texts after '='.\n" +
                   "Lines starting with # are comments. Leave a text empty to keep the original. \\n is a line break.\n" +
                   "{0}, {1} ... are values filled in by the mod: keep them.\n" +
                   (kind == "game" ? "@base = the game language shown for anything not translated (en, th, ja, id or zh-HK).\n" : string.Empty) +
                   "Choose the language in the game's Options or the mod settings (F5). Saved changes load within a few seconds.";
        }

        /// <summary>The language whose game-text file should be applied right now.</summary>
        public static string ActiveGameTextLanguage()
        {
            string wanted = Plugin.Settings.GameLocale.Value;
            if (!string.IsNullOrEmpty(wanted) && HasGameText(wanted.ToLowerInvariant())) return wanted.ToLowerInvariant();
            Locale cur = GameLanguage.Selected;
            if (cur == null) return null;
            string code = cur.Identifier.Code.ToLowerInvariant();
            if (HasGameText(code)) return code;
            string norm = Loc.NormalizeLanguage(code);
            return HasGameText(norm) ? norm : null;
        }

        /// <summary>Restores previously replaced entries, then writes the active language's entries into the loaded tables.</summary>
        public static void ApplyGameText()
        {
            _gameTextDirty = false;
            Game.GameText.ForgetTexts();
            foreach (var (table, key, original, added) in Applied)
            {
                if (table == null) continue;
                StringTableEntry e = table.GetEntry(key);
                if (e == null) continue;
                if (added) table.RemoveEntry(key);
                else e.Value = original;
            }
            Applied.Clear();
            Unplaced.Clear();
            string lang = ActiveGameTextLanguage();
            if (lang != null && GameFiles.TryGetValue(lang, out LangFile file))
            {
                var tables = new Dictionary<string, StringTable>(StringComparer.Ordinal);
                foreach (var kv in file.Entries)
                {
                    int slash = kv.Key.IndexOf('/');
                    if (slash <= 0) continue;
                    string tableName = kv.Key.Substring(0, slash);
                    string key = kv.Key.Substring(slash + 1);
                    if (!tables.TryGetValue(tableName, out StringTable table))
                    {
                        try { table = LocalizationSettings.StringDatabase.GetTable(tableName); }
                        catch { table = null; }
                        tables[tableName] = table;
                    }
                    if (table == null)
                    {
                        Unplaced[kv.Key] = kv.Value;
                        continue;
                    }
                    StringTableEntry entry = table.GetEntry(key);
                    if (entry == null)
                    {
                        table.AddEntry(key, kv.Value);
                        Applied.Add((table, key, null, true));
                    }
                    else
                    {
                        Applied.Add((table, key, entry.Value, false));
                        entry.Value = kv.Value;
                    }
                }
                Log.Info("Applied " + Applied.Count + " game text entries for " + lang + (Unplaced.Count > 0 ? " (" + Unplaced.Count + " in tables the game lacks for this language)" : string.Empty));
            }
            RefreshGameText();
        }

        /// <summary>Makes the game redraw localized text without changing scenes.</summary>
        public static void RefreshGameText()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { LocalizationManager.PreloadTables(); }
            catch (Exception e) { Log.Debug("Preloading the game's tables failed: " + e.Message); }
            long tables = sw.ElapsedMilliseconds;
            // Every listener on its own: one that fails (a destroyed object still subscribed, a table not
            // loaded yet) must not keep the rest of the game's text in the old language.
            int failed = 0;
            string first = null;
            try
            {
                FieldInfo evt = typeof(LocalizationManager).GetField("TablesReloaded", BindingFlags.NonPublic | BindingFlags.Static);
                if (evt?.GetValue(null) is Action reloaded)
                {
                    foreach (Delegate d in reloaded.GetInvocationList())
                    {
                        try { ((Action)d)(); }
                        catch (Exception e)
                        {
                            failed++;
                            if (first == null) first = d.Method.DeclaringType?.Name + "." + d.Method.Name + ": " + e.Message;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("Refreshing game text failed: " + e.Message);
            }
            long listeners = sw.ElapsedMilliseconds - tables;
            failed += RefreshShownText(ref first);
            if (failed > 0) Log.Debug("Refreshing game text: " + failed + " listener(s) failed; first " + first);
            Log.Debug("Game text redrawn: tables " + tables + " ms, listeners " + listeners + " ms, text components " + (sw.ElapsedMilliseconds - tables - listeners) + " ms");
        }

        /// <summary>
        /// Redraws the scene's localized text components (labels such as the main menu buttons) with the
        /// current language. Returns how many failed.
        /// </summary>
        public static int RefreshShownText(ref string firstError)
        {
            int failed = 0;
            try
            {
                // Scene objects only (also hidden ones): searching every loaded asset as well is much slower.
                foreach (LocalizeStringEvent lse in UnityEngine.Object.FindObjectsByType<LocalizeStringEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (lse == null || !lse.gameObject.scene.IsValid()) continue;
                    try { lse.RefreshString(); }
                    catch (Exception e)
                    {
                        failed++;
                        if (firstError == null) firstError = lse.gameObject.name + ": " + e.Message;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("Refreshing the game's labels failed: " + e.Message);
            }
            return failed;
        }
    }

    /// <summary>
    /// The game ships five string tables but its dropdown offers only English and Thai. The mod lets
    /// the player pick any of them, plus languages added by translation files, remembers the choice,
    /// and makes its own messages follow the game's language.
    /// </summary>
    internal static class GameLanguage
    {
        private static bool _subscribed;
        private static bool _loggedLocales;
        private static float _nextCheck;

        public static IList<Locale> Locales
        {
            get
            {
                try { return LocalizationSettings.AvailableLocales?.Locales; }
                catch { return null; }
            }
        }

        public static Locale Selected
        {
            get
            {
                try { return LocalizationSettings.SelectedLocale; }
                catch { return null; }
            }
        }

        public static string DisplayName(Locale locale)
        {
            if (locale == null) return "?";
            // The game's own languages get fixed native names (Windows would name zh-HK in Simplified Chinese).
            string norm = Loc.NormalizeLanguage(locale.Identifier.Code);
            if (Loc.SupportedLanguages.Contains(norm)) return LanguageFiles.DisplayName(norm);
            try
            {
                var ci = locale.Identifier.CultureInfo;
                if (ci != null && !string.IsNullOrEmpty(ci.NativeName)) return ci.NativeName;
            }
            catch
            {
                // Some identifiers have no CultureInfo on Mono.
            }
            return string.IsNullOrEmpty(locale.LocaleName) ? locale.Identifier.Code : locale.LocaleName;
        }

        /// <summary>Every selectable language: the game's locales, then languages from translation files.</summary>
        public static List<(string code, string name)> Choices()
        {
            var list = new List<(string, string)>();
            IList<Locale> locales = Locales;
            if (locales != null)
            {
                foreach (Locale l in locales) list.Add((l.Identifier.Code, DisplayName(l)));
            }
            foreach (string extra in LanguageFiles.ExtraLanguages())
            {
                if (!list.Any(c => string.Equals(c.Item1, extra, StringComparison.OrdinalIgnoreCase))) list.Add((extra, LanguageFiles.DisplayName(extra)));
            }
            return list;
        }

        public static int CurrentChoiceIndex(List<(string code, string name)> choices)
        {
            string wanted = Plugin.Settings.GameLocale.Value;
            if (!string.IsNullOrEmpty(wanted))
            {
                int i = choices.FindIndex(c => string.Equals(c.code, wanted, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) return i;
            }
            Locale cur = Selected;
            return cur == null ? -1 : choices.FindIndex(c => c.code == cur.Identifier.Code);
        }

        public static void Update()
        {
            // Every frame until the language is in place (the first screen waits for it), then twice a second.
            if (Ready && Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                if (!LocalizationSettings.InitializationOperation.IsDone) return;
            }
            catch
            {
                return;
            }
            if (!_subscribed)
            {
                _subscribed = true;
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
                SyncModLanguage();
            }
            if (!_loggedLocales && Locales != null)
            {
                _loggedLocales = true;
                Log.Info("Game locales: " + string.Join(", ", Locales.Select(l => l.Identifier.Code).ToArray()) + "; selected " + Selected?.Identifier.Code
                    + " (ready " + Time.realtimeSinceStartup.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s after the start, frame " + Time.frameCount + ")");
            }
            ApplyOverride();
            // A language the override has just switched to is in place once the game's localization has
            // loaded it (it loads again after every switch); before that, its labels still show the previous
            // language, and the first screen would be read in that one.
            bool loaded;
            try
            {
                loaded = LocalizationSettings.InitializationOperation.IsDone;
            }
            catch
            {
                loaded = false;
            }
            if (!Ready && loaded && Time.unscaledTime >= _switchPendingUntil && OverrideSettled())
            {
                Ready = true;
                SyncModLanguage();
                _fallbackPending = true;
                // The game has all of its Thai and English text, so the mod's language work (templates,
                // translation files, English fallback) can wait until the main menu has been read; the
                // other languages need it for that first screen already.
                if (SelectedIsComplete) _startupWorkNotBefore = Time.unscaledTime + 1.5f;
            }
            if (_fallbackPending && Ready && !StartupWorkDeferred)
            {
                // After Unity has released the previous language's tables: load the English fallback
                // tables, then let the game redraw its text with them.
                _fallbackPending = false;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Patches.GameTextFallbackPatch.PreloadEnglish();
                long english = sw.ElapsedMilliseconds;
                LanguageFiles.RefreshGameText();
                Log.Debug("English fallback tables " + english + " ms, game text redrawn " + (sw.ElapsedMilliseconds - english) + " ms");
            }
        }

        private static bool _fallbackPending;
        private static float _startupWorkNotBefore = -1f;

        /// <summary>
        /// Right after the start, the mod's own language work waits a moment so that the first screen is
        /// read without delay (only for Thai and English, which the game has complete).
        /// </summary>
        public static bool StartupWorkDeferred => Time.unscaledTime < _startupWorkNotBefore;

        /// <summary>The game has all of its text in the selected language (Thai and English).</summary>
        public static bool SelectedIsComplete
        {
            get
            {
                string code = Selected != null ? Selected.Identifier.Code : string.Empty;
                if (!string.IsNullOrEmpty(Plugin.Settings.GameLocale.Value) && !string.Equals(Plugin.Settings.GameLocale.Value, code, StringComparison.OrdinalIgnoreCase)) return false;
                return code.StartsWith("th", StringComparison.OrdinalIgnoreCase) || code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// True once the game's localization has started and a language chosen through the mod is in place,
        /// so messages spoken from now on are in the player's language.
        /// </summary>
        public static bool Ready { get; private set; }

        // Set when the mod itself changes the locale (startup override): such changes are not announced.
        private static bool _quietChange;

        // After the override switched the locale: the game's localization reports the new one at once, but
        // announces the change (and its labels start loading the new text) only later in the frame. The
        // language is in place once that has happened (at the latest after 2 seconds).
        private static float _switchPendingUntil = -1f;

        private static bool OverrideSettled()
        {
            string wanted = Plugin.Settings.GameLocale.Value;
            if (string.IsNullOrEmpty(wanted)) return true;
            Locale current = Selected;
            return current != null && current.Identifier.Code.StartsWith(TargetLocaleCode(wanted), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Locale the game must show for the player's choice (a translation-only language shows its base).</summary>
        private static string TargetLocaleCode(string wanted)
        {
            IList<Locale> locales = Locales;
            if (locales != null && locales.Any(l => string.Equals(l.Identifier.Code, wanted, StringComparison.OrdinalIgnoreCase))) return wanted;
            if (!LanguageFiles.HasGameText(wanted.ToLowerInvariant())) return "en";
            string baseCode = LanguageFiles.BaseLocaleOf(wanted.ToLowerInvariant());
            // A base the game does not have (a typo in @base): English, as for a file without one. Otherwise
            // the language would never be in place, and the mod would wait for it at every start.
            if (locales != null && !locales.Any(l => l.Identifier.Code.StartsWith(baseCode, StringComparison.OrdinalIgnoreCase)))
            {
                if (BadBases.Add(wanted + "/" + baseCode)) Log.Warn("Translation " + wanted + ": the game has no language \"" + baseCode + "\" (@base); English is used.");
                return "en";
            }
            return baseCode;
        }

        private static readonly HashSet<string> BadBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Re-applies the player's mod-chosen language once the game's own startup selection is done.</summary>
        private static void ApplyOverride()
        {
            string wanted = Plugin.Settings.GameLocale.Value;
            if (string.IsNullOrEmpty(wanted)) return;
            LanguageControl lc = LanguageControl.Instance;
            if (lc != null && lc.active) return;
            string target = TargetLocaleCode(wanted);
            // The game's own setting follows, so its Options dropdown shows this language and it saves it.
            SyncLanguageControl(target);
            Locale current = Selected;
            if (current != null && current.Identifier.Code.StartsWith(target, StringComparison.OrdinalIgnoreCase)) return;
            Locale l = Locales?.FirstOrDefault(x => x.Identifier.Code.StartsWith(target, StringComparison.OrdinalIgnoreCase));
            if (l == null) return;
            _quietChange = true;
            _switchPendingUntil = Time.unscaledTime + 2f;
            LocalizationSettings.SelectedLocale = l;
        }

        // Until then a locale change was asked for (and announced) by Select, so the locale event stays quiet.
        private static float _selectedAnnouncedUntil = -1f;

        /// <summary>
        /// Shows the game in the chosen language (the mod's messages follow it when the mod language is
        /// Automatic) and says the new language at once, whatever the game does with the change.
        /// </summary>
        public static void Select(string code, string name)
        {
            Plugin.Settings.GameLocale.Value = code;
            string target = TargetLocaleCode(code);
            Locale l = Locales?.FirstOrDefault(x => x.Identifier.Code.StartsWith(target, StringComparison.OrdinalIgnoreCase));
            SyncLanguageControl(target);
            // The game finishes the change a frame or more later; its event must not say it again.
            _selectedAnnouncedUntil = Time.unscaledTime + 3f;
            try
            {
                if (l != null && l != Selected) LocalizationSettings.SelectedLocale = l;
                else OnLocaleChanged(Selected);
            }
            catch (Exception e)
            {
                Log.Warn("Changing the game language failed: " + e.Message);
            }
            // With the mod language on Automatic, the message is already in the new language.
            SyncModLanguage();
            SpeechService.Say(Loc.Get("language_changed", name), true);
        }

        /// <summary>
        /// The language the mod can speak before the game's localization has started: the mod language
        /// setting, the language chosen through the mod, or the game's own saved choice (English or
        /// Thai). Null when only the game can tell.
        /// </summary>
        public static string EarlyLanguage()
        {
            string setting = Plugin.Settings.ModLanguage.Value;
            if (!string.IsNullOrEmpty(setting) && !string.Equals(setting, "Auto", StringComparison.OrdinalIgnoreCase)) return setting;
            string wanted = Plugin.Settings.GameLocale.Value;
            if (!string.IsNullOrEmpty(wanted)) return wanted;
            try
            {
                if (PlayerPrefs.HasKey("Language")) return PlayerPrefs.GetInt("Language") == (int)Language.Thai ? "th" : "en";
            }
            catch (Exception e)
            {
                Log.Debug("Could not read the game's language choice: " + e.Message);
            }
            return null;
        }

        /// <summary>
        /// The screen's localized labels are still loading their text for the current language: right after a
        /// language switch they keep the previous language's text until then (asking them to redraw does
        /// nothing before that).
        /// </summary>
        public static bool LabelsLoading()
        {
            try
            {
                foreach (LocalizeStringEvent lse in UnityEngine.Object.FindObjectsByType<LocalizeStringEvent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (lse == null || !lse.isActiveAndEnabled || lse.StringReference == null) continue;
                    var handle = lse.StringReference.CurrentLoadingOperationHandle;
                    if (handle.IsValid() && !handle.IsDone) return true;
                }
            }
            catch (Exception e)
            {
                Log.Debug("Checking the labels failed: " + e.Message);
            }
            return false;
        }

        /// <summary>Called when the player uses the game's own dropdown: their choice wins over the mod's.</summary>
        public static void ClearOverride()
        {
            if (!string.IsNullOrEmpty(Plugin.Settings.GameLocale.Value)) Plugin.Settings.GameLocale.Value = string.Empty;
        }

        /// <summary>A language is chosen through the mod (it wins over the game's own setting).</summary>
        public static bool Overridden => !string.IsNullOrEmpty(Plugin.Settings.GameLocale.Value);

        /// <summary>The game shows <paramref name="language"/> (one of the two its own dropdown offers).</summary>
        public static bool Shows(Language language)
        {
            Locale cur = Selected;
            return cur != null && cur.Identifier.Code.StartsWith(language == Language.Thai ? "th" : "en", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The game's own language setting (what its Options dropdown shows, and the choice it saves) follows
        /// a language chosen through the mod when the game has it there: English or Thai.
        /// </summary>
        private static void SyncLanguageControl(string target)
        {
            try
            {
                Language? l = target.StartsWith("th", StringComparison.OrdinalIgnoreCase) ? Language.Thai
                    : target.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? Language.English : (Language?)null;
                if (l.HasValue && LanguageControl.CurrentLanguage != l.Value) LanguageControl.CurrentLanguage = l.Value;
            }
            catch (Exception e)
            {
                Log.Debug("Could not sync LanguageControl: " + e.Message);
            }
        }

        /// <summary>
        /// After "Reset all settings": no language is chosen through the mod any more, so the game shows its
        /// own language setting again, without a translation file's text left in its tables.
        /// </summary>
        public static void BackToGameLanguage()
        {
            LanguageFiles.MarkGameTextDirty();
            try
            {
                Language own = LanguageControl.CurrentLanguage;
                if (Shows(own)) return;
                Locale l = Locales?.FirstOrDefault(x => x.Identifier.Code.StartsWith(own == Language.Thai ? "th" : "en", StringComparison.OrdinalIgnoreCase));
                if (l == null) return;
                // "Settings reset" says it; the language event stays quiet.
                _quietChange = true;
                LocalizationSettings.SelectedLocale = l;
            }
            catch (Exception e)
            {
                Log.Debug("Could not show the game's own language again: " + e.Message);
            }
        }

        private static void OnLocaleChanged(Locale locale)
        {
            Log.Debug("Game language changed to " + locale?.Identifier.Code + " (frame " + Time.frameCount + ", " + Time.realtimeSinceStartup.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s)");
            _switchPendingUntil = -1f;
            _fallbackPending = true;
            Game.GameText.ForgetTexts();
            SyncModLanguage();
            LanguageFiles.MarkGameTextDirty();
            LanguageFiles.ApplyGameText();
            bool quiet = _quietChange || !Ready || Time.unscaledTime < _selectedAnnouncedUntil;
            _quietChange = false;
            if (quiet) return;
            string wanted = Plugin.Settings.GameLocale.Value;
            string name = !string.IsNullOrEmpty(wanted) && Loc.IsExternalOnly(wanted.ToLowerInvariant()) ? LanguageFiles.DisplayName(wanted) : DisplayName(locale);
            SpeechService.Say(Loc.Get("language_changed", name), true);
        }

        public static void SyncModLanguage()
        {
            string setting = Plugin.Settings.ModLanguage.Value;
            if (!string.IsNullOrEmpty(setting) && !string.Equals(setting, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                Loc.SetLanguage(setting);
                return;
            }
            string wanted = Plugin.Settings.GameLocale.Value;
            if (!string.IsNullOrEmpty(wanted))
            {
                Loc.SetLanguage(wanted);
                return;
            }
            Locale l = Selected;
            if (l != null) Loc.SetLanguage(l.Identifier.Code);
        }
    }

    /// <summary>Navigation item that cycles through every language: the game's and those added by translation files.</summary>
    internal sealed class GameLanguageItem : NavItem
    {
        public override string Key => "game_language";
        public override string Label() => Loc.Get("label_language");
        public override string Role() => Loc.Get("role_combobox");

        public override string Value()
        {
            var choices = GameLanguage.Choices();
            int i = GameLanguage.CurrentChoiceIndex(choices);
            if (i < 0) return "?";
            return choices[i].name + " (" + (i + 1) + "/" + choices.Count + ")";
        }

        public override string Detail() => Loc.Get("hint_language");

        /// <summary>Left and right: the previous or next language (round the ends when wrapping is on).</summary>
        public override bool Adjust(int direction, bool large) => Step(direction, NavSettings.Wrap);

        /// <summary>Enter: the next language, always going round.</summary>
        public override bool Activate() => Step(1, wrap: true);

        private bool Step(int direction, bool wrap)
        {
            var choices = GameLanguage.Choices();
            if (choices.Count == 0) return true;
            int i = ChoiceStep.Step(GameLanguage.CurrentChoiceIndex(choices), choices.Count, direction, wrap, out bool atEdge);
            if (atEdge)
            {
                Audio.AudioCues.Play(Audio.Cue.Edge);
                SpeechService.Say(Value(), true);
                return true;
            }
            GameLanguage.Select(choices[i].code, choices[i].name);
            return true;
        }
    }
}
