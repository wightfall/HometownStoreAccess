using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

        public static bool TryGetUnplaced(string tableName, string key, out string text) => Unplaced.TryGetValue(tableName + "/" + key, out text);
        private static float _nextPoll;
        private static bool _templatesWritten;
        private static bool _gameTextDirty = true;

        public static string Folder => Path.Combine(Plugin.PluginDirectory, "lang");

        public static string DisplayName(string code)
        {
            switch ((code ?? string.Empty).ToLowerInvariant())
            {
                case "en": return "English";
                case "th": return "ไทย";
                case "ja": return "日本語";
                case "id": return "Bahasa Indonesia";
                case "zh": return "繁體中文";
            }
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
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 2f;
            try
            {
                Directory.CreateDirectory(Folder);
                if (ScanForChanges()) Reload();
                if (!_templatesWritten && LocalizationReady()) WriteTemplates();
                if (_gameTextDirty && LocalizationReady()) ApplyGameText();
            }
            catch (Exception e)
            {
                Log.Warn("Language files: " + e.Message);
                _nextPoll = Time.unscaledTime + 10f;
            }
        }

        public static void MarkGameTextDirty() => _gameTextDirty = true;

        private static bool LocalizationReady()
        {
            try { return LocalizationSettings.InitializationOperation.IsDone && LocalizationSettings.SelectedLocale != null; }
            catch { return false; }
        }

        private static bool ScanForChanges()
        {
            bool changed = false;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(Folder, "*.txt"))
            {
                string name = Path.GetFileName(file);
                if (!IsTranslationFileName(name)) continue;
                seen.Add(file);
                DateTime t = File.GetLastWriteTimeUtc(file);
                if (!Stamps.TryGetValue(file, out DateTime old) || old != t)
                {
                    Stamps[file] = t;
                    changed = true;
                }
            }
            foreach (string gone in Stamps.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                Stamps.Remove(gone);
                changed = true;
            }
            return changed;
        }

        private static void Reload()
        {
            Loc.ClearExternal();
            GameFiles.Clear();
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
                    string code = name.Substring(0, name.Length - ".game.txt".Length).ToLowerInvariant();
                    GameFiles[code] = f;
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
            foreach (LocalizationTable t in Enum.GetValues(typeof(LocalizationTable)))
            {
                string tableName = t.GetTableName();
                StringTable table = en != null ? LocalizationSettings.StringDatabase.GetTable(tableName, en) : null;
                if (table == null) continue;
                foreach (StringTableEntry e in table.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
                {
                    if (e != null && !string.IsNullOrEmpty(e.Key)) entries.Add(new KeyValuePair<string, string>(tableName + "/" + e.Key, e.Value ?? string.Empty));
                }
            }
            if (entries.Count > 0) WriteIfChanged(Path.Combine(Folder, "template.game.txt"), LangFile.BuildTemplate(TemplateHeader("game"), entries));
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
            try
            {
                LocalizationManager.PreloadTables();
                FieldInfo evt = typeof(LocalizationManager).GetField("TablesReloaded", BindingFlags.NonPublic | BindingFlags.Static);
                (evt?.GetValue(null) as Action)?.Invoke();
                foreach (LocalizeStringEvent lse in Resources.FindObjectsOfTypeAll<LocalizeStringEvent>())
                {
                    if (lse != null && lse.gameObject.scene.IsValid()) lse.RefreshString();
                }
            }
            catch (Exception e)
            {
                Log.Warn("Refreshing game text failed: " + e.Message);
            }
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
            if (Time.unscaledTime < _nextCheck) return;
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
                Log.Info("Game locales: " + string.Join(", ", Locales.Select(l => l.Identifier.Code).ToArray()) + "; selected " + Selected?.Identifier.Code);
            }
            ApplyOverride();
            if (!Ready && OverrideSettled())
            {
                Ready = true;
                SyncModLanguage();
                _fallbackPending = true;
            }
            if (_fallbackPending && Ready)
            {
                // After Unity has released the previous language's tables: load the English fallback
                // tables, then let the game redraw its text with them.
                _fallbackPending = false;
                Patches.GameTextFallbackPatch.PreloadEnglish();
                LanguageFiles.RefreshGameText();
            }
        }

        private static bool _fallbackPending;

        /// <summary>
        /// True once the game's localization has started and a language chosen through the mod is in place,
        /// so messages spoken from now on are in the player's language.
        /// </summary>
        public static bool Ready { get; private set; }

        // Set when the mod itself changes the locale (startup override): such changes are not announced.
        private static bool _quietChange;

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
            return LanguageFiles.HasGameText(wanted.ToLowerInvariant()) ? LanguageFiles.BaseLocaleOf(wanted.ToLowerInvariant()) : "en";
        }

        /// <summary>Re-applies the player's mod-chosen language once the game's own startup selection is done.</summary>
        private static void ApplyOverride()
        {
            string wanted = Plugin.Settings.GameLocale.Value;
            if (string.IsNullOrEmpty(wanted)) return;
            LanguageControl lc = LanguageControl.Instance;
            if (lc != null && lc.active) return;
            string target = TargetLocaleCode(wanted);
            Locale current = Selected;
            if (current != null && current.Identifier.Code.StartsWith(target, StringComparison.OrdinalIgnoreCase)) return;
            Locale l = Locales?.FirstOrDefault(x => x.Identifier.Code.StartsWith(target, StringComparison.OrdinalIgnoreCase));
            if (l == null) return;
            _quietChange = true;
            LocalizationSettings.SelectedLocale = l;
        }

        public static void Select(string code)
        {
            Plugin.Settings.GameLocale.Value = code;
            string target = TargetLocaleCode(code);
            Locale l = Locales?.FirstOrDefault(x => x.Identifier.Code.StartsWith(target, StringComparison.OrdinalIgnoreCase));
            try
            {
                if (target.StartsWith("th")) LanguageControl.CurrentLanguage = Language.Thai;
                else if (target.StartsWith("en")) LanguageControl.CurrentLanguage = Language.English;
            }
            catch (Exception e)
            {
                Log.Debug("Could not sync LanguageControl: " + e.Message);
            }
            if (l != null && l != Selected) LocalizationSettings.SelectedLocale = l;
            else OnLocaleChanged(Selected);
        }

        /// <summary>Called when the player uses the game's own dropdown: their choice wins over the mod's.</summary>
        public static void ClearOverride()
        {
            if (!string.IsNullOrEmpty(Plugin.Settings.GameLocale.Value)) Plugin.Settings.GameLocale.Value = string.Empty;
        }

        private static void OnLocaleChanged(Locale locale)
        {
            _fallbackPending = true;
            SyncModLanguage();
            LanguageFiles.MarkGameTextDirty();
            LanguageFiles.ApplyGameText();
            bool quiet = _quietChange || !Ready;
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

        public override bool Adjust(int direction, bool large)
        {
            var choices = GameLanguage.Choices();
            if (choices.Count == 0) return true;
            int i = Math.Max(0, GameLanguage.CurrentChoiceIndex(choices));
            i = ((i + direction) % choices.Count + choices.Count) % choices.Count;
            GameLanguage.Select(choices[i].code);
            return true;
        }

        public override bool Activate() => Adjust(1, false);
    }
}
