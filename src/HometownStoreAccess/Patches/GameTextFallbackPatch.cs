using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace HometownStoreAccess.Patches
{
    /// <summary>
    /// Unity builds every localized string in LocalizedStringDatabase.GenerateLocalizedString. The demo
    /// ships Japanese, Chinese and Indonesian with most text untranslated, and some tables missing
    /// entirely, so the game would show "No translation found for ..." there. When the selected language
    /// has no text for an entry, this uses the player's translation file text if there is one, and the
    /// English original otherwise.
    /// </summary>
    [HarmonyPatch(typeof(LocalizedStringDatabase), "GenerateLocalizedString")]
    internal static class GameTextFallbackPatch
    {
        private static Locale _english;
        private static bool _failed;
        // Unity releases every cached table when the language changes, so the English tables are loaded
        // again (by GameLanguage, right after each change) and kept here for the fallback.
        private static readonly Dictionary<string, StringTable> EnglishTables = new Dictionary<string, StringTable>(StringComparer.Ordinal);

        /// <summary>Loads the English tables used as fallback; nothing to do while English is selected.</summary>
        public static void PreloadEnglish()
        {
            EnglishTables.Clear();
            Locale en = English;
            if (en == null || IsEnglishSelected()) return;
            foreach (LocalizationTable t in Enum.GetValues(typeof(LocalizationTable)))
            {
                string name = t.GetTableName();
                try
                {
                    var op = LocalizationSettings.StringDatabase.GetTableAsync(name, en);
                    StringTable table = op.IsDone ? op.Result : op.WaitForCompletion();
                    if (table != null) EnglishTables[name] = table;
                }
                catch (Exception e)
                {
                    Log.Debug("English table " + name + " not loaded: " + e.Message);
                }
            }
            Log.Debug("Loaded " + EnglishTables.Count + " English tables for the game text fallback");
        }

        private static StringTable LoadedEnglishTable(string tableName)
        {
            if (tableName != null && EnglishTables.TryGetValue(tableName, out StringTable t) && t != null) return t;
            return null;
        }

        private static Locale English
        {
            get
            {
                if (_english == null)
                {
                    _english = LocalizationSettings.AvailableLocales?.Locales?.FirstOrDefault(l => l.Identifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase));
                }
                return _english;
            }
        }

        private static void Postfix(StringTable table, StringTableEntry entry, TableReference tableReference, TableEntryReference tableEntryReference, Locale locale, IList<object> arguments, ref string __result)
        {
            if (_failed || (entry != null && !string.IsNullOrEmpty(entry.Value))) return;
            try
            {
                Locale en = English;
                if (en == null) return;
                StringTable enTable = EnglishTable(table, tableReference, en);
                if (enTable == null) return;
                string key = tableEntryReference.ResolveKeyName(enTable.SharedData);
                if (string.IsNullOrEmpty(key)) return;

                if (LanguageFiles.TryGetUnplaced(enTable.TableCollectionName, key, out string translated))
                {
                    __result = Format(translated, arguments);
                    return;
                }
                if (locale != null && locale.Identifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return;
                StringTableEntry enEntry = enTable.GetEntryFromReference(tableEntryReference);
                if (enEntry == null || string.IsNullOrEmpty(enEntry.Value)) return;
                string english = enEntry.GetLocalizedString(en, arguments, null);
                if (!string.IsNullOrEmpty(english)) __result = english;
            }
            catch (Exception e)
            {
                // Never break the game's text: on an unexpected error keep Unity's own behaviour from now on.
                _failed = true;
                Log.Warn("Game text fallback disabled: " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>The English table of the same collection, only when it is already loaded (never blocks).</summary>
        private static StringTable EnglishTable(StringTable table, TableReference tableReference, Locale en)
        {
            if (table != null && table.LocaleIdentifier == en.Identifier) return table;
            string name = table != null ? table.TableCollectionName
                : tableReference.ReferenceType == TableReference.Type.Name ? tableReference.TableCollectionName : null;
            StringTable loaded = LoadedEnglishTable(name);
            if (loaded != null) return loaded;
            var op = LocalizationSettings.StringDatabase.GetTableAsync(tableReference, en);
            return op.IsDone ? op.Result : null;
        }

        private static string Format(string text, IList<object> arguments)
        {
            if (arguments == null || arguments.Count == 0 || text.IndexOf('{') < 0) return text;
            try
            {
                return LocalizationSettings.StringDatabase.SmartFormatter.Format(arguments, text);
            }
            catch (Exception)
            {
                return text;
            }
        }

        /// <summary>The English text of an entry, from the already loaded English table; null when there is none.</summary>
        internal static string EnglishText(string tableName, string key)
        {
            Locale en = English;
            if (en == null) return null;
            StringTable table = LoadedEnglishTable(tableName);
            if (table == null)
            {
                var op = LocalizationSettings.StringDatabase.GetTableAsync(tableName, en);
                if (!op.IsDone || op.Result == null) return null;
                table = op.Result;
            }
            string v = table.GetEntry(key)?.Value;
            return string.IsNullOrEmpty(v) ? null : v;
        }

        internal static bool IsEnglishSelected()
        {
            Locale cur = LocalizationSettings.SelectedLocale;
            return cur == null || cur.Identifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The game reads most of its text (names, descriptions, dialog lines) through its own
    /// LocalizationManager.GetString, straight from the loaded tables, and gets "" or "Key 'x' not found"
    /// when the selected language lacks the entry. Same fallback as above: translation file, then English.
    /// </summary>
    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.GetString), typeof(LocalizationTable), typeof(string))]
    internal static class GameManagerTextFallbackPatch
    {
        private static bool _failed;

        private static void Postfix(LocalizationTable tableEnum, string key, ref string __result)
        {
            if (_failed || string.IsNullOrEmpty(key)) return;
            if (!string.IsNullOrEmpty(__result) && !__result.StartsWith("Key '", StringComparison.Ordinal) && !__result.StartsWith("Table '", StringComparison.Ordinal)) return;
            try
            {
                string tableName = tableEnum.GetTableName();
                if (LanguageFiles.TryGetUnplaced(tableName, key, out string translated))
                {
                    __result = translated;
                    return;
                }
                if (GameTextFallbackPatch.IsEnglishSelected()) return;
                string english = GameTextFallbackPatch.EnglishText(tableName, key);
                if (english != null) __result = english;
            }
            catch (Exception e)
            {
                _failed = true;
                Log.Warn("Game text fallback (LocalizationManager) disabled: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
