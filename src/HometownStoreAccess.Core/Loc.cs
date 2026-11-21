using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// Localization for the mod's own text. Five languages are built in (the game's own: English,
    /// Thai, Japanese, Indonesian, Traditional Chinese). Translators can add any other language, or
    /// correct a built-in one, with a plain text file loaded at runtime (see <see cref="LangFile"/>);
    /// those entries override the built-in ones and are reloaded while the game runs.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Thai = "th";
        public const string Japanese = "ja";
        public const string Indonesian = "id";
        public const string Chinese = "zh";

        public static readonly string[] SupportedLanguages = { English, Thai, Japanese, Indonesian, Chinese };

        private static readonly Dictionary<string, Dictionary<string, string>> External = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ExternalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string _language = English;

        public static string Language => _language;

        public static event Action LanguageChanged;

        /// <summary>Built-in languages plus every language added by a translation file.</summary>
        public static IEnumerable<string> AvailableLanguages => SupportedLanguages.Concat(External.Keys.Where(k => !SupportedLanguages.Contains(k, StringComparer.OrdinalIgnoreCase)));

        /// <summary>
        /// Maps a locale code ("en", "th-TH", "zh-HK", "es") to a mod language. Codes of languages added by
        /// translation files are kept as they are; other unknown codes fall back to English.
        /// </summary>
        public static string NormalizeLanguage(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode)) return English;
            string c = localeCode.Trim().ToLowerInvariant().Replace('_', '-');
            if (External.ContainsKey(c) && !SupportedLanguages.Contains(c)) return External.Keys.First(k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase));
            if (c.StartsWith("th")) return Thai;
            if (c.StartsWith("ja") || c.StartsWith("jp")) return Japanese;
            if (c.StartsWith("id") || c.StartsWith("in")) return Indonesian;
            if (c.StartsWith("zh")) return Chinese;
            if (c.StartsWith("en")) return English;
            int dash = c.IndexOf('-');
            if (dash > 0)
            {
                string baseCode = c.Substring(0, dash);
                if (External.ContainsKey(baseCode)) return External.Keys.First(k => string.Equals(k, baseCode, StringComparison.OrdinalIgnoreCase));
            }
            return English;
        }

        public static void SetLanguage(string localeCode)
        {
            string lang = NormalizeLanguage(localeCode);
            if (lang == _language) return;
            _language = lang;
            LanguageChanged?.Invoke();
        }

        /// <summary>Adds or replaces the entries of a language loaded from a translation file.</summary>
        public static void SetExternal(string language, IDictionary<string, string> entries, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(language)) return;
            string code = language.Trim().ToLowerInvariant();
            External[code] = new Dictionary<string, string>(entries ?? new Dictionary<string, string>());
            if (!string.IsNullOrWhiteSpace(displayName)) ExternalNames[code] = displayName.Trim();
            LanguageChanged?.Invoke();
        }

        public static void ClearExternal()
        {
            External.Clear();
            ExternalNames.Clear();
        }

        /// <summary>Name a translation file gave its language (e.g. "Español"), or null.</summary>
        public static string ExternalName(string language) => language != null && ExternalNames.TryGetValue(language, out string n) ? n : null;

        public static bool IsExternalOnly(string language) => language != null && External.ContainsKey(language) && !SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);

        /// <summary>Ending of the controller version of a text ("tg_build.pad").</summary>
        public const string PadSuffix = ".pad";

        /// <summary>
        /// True while the player uses a controller. Texts that have a controller version (the same key
        /// with <see cref="PadSuffix"/>) then give that version, so instructions name the controller's
        /// buttons instead of keyboard keys.
        /// </summary>
        public static bool PadMode { get; set; }

        /// <summary>The key actually looked up: the controller version when <see cref="PadMode"/> is on and one exists.</summary>
        public static string EffectiveKey(string key)
        {
            if (!PadMode || string.IsNullOrEmpty(key) || key.EndsWith(PadSuffix, StringComparison.Ordinal)) return key;
            string pad = key + PadSuffix;
            bool external = External.TryGetValue(_language, out var ext) && ext.ContainsKey(pad);
            return external || Has(_language, pad) || Has(English, pad) ? pad : key;
        }

        /// <summary>Returns the translated string for <paramref name="key"/>, formatted with args.</summary>
        public static string Get(string key, params object[] args)
        {
            string template = Lookup(_language, EffectiveKey(key));
            if (args == null || args.Length == 0) return template;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        /// <summary>True when a built-in translation exists for the key in the given language (no fallback).</summary>
        public static bool Has(string language, string key)
        {
            return Strings.Tables.TryGetValue(language, out var table) && table.ContainsKey(key);
        }

        internal static string Lookup(string language, string key)
        {
            if (External.TryGetValue(language, out var ext) && ext.TryGetValue(key, out var e) && !string.IsNullOrEmpty(e)) return e;
            if (Strings.Tables.TryGetValue(language, out var table) && table.TryGetValue(key, out var value)) return value;
            if (Strings.Tables.TryGetValue(English, out var en) && en.TryGetValue(key, out var fallback)) return fallback;
            return key;
        }

        public static IEnumerable<string> AllKeys => Strings.Tables[English].Keys;

        /// <summary>English text of a key (used to write translation templates).</summary>
        public static string EnglishText(string key) => Strings.Tables[English].TryGetValue(key, out string v) ? v : key;
    }
}
