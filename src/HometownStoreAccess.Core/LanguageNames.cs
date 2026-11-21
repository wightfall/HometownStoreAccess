namespace HometownStoreAccess.Core
{
    /// <summary>
    /// Spoken names of the game's languages. Each is said in its own language, and those not written in
    /// Latin letters also in English: when the mod follows the game's language, the voice in use may not
    /// read Thai, Japanese or Chinese letters, and the player must still hear which language was chosen
    /// (and find the way back).
    /// </summary>
    public static class LanguageNames
    {
        /// <summary>The name for a language code (en, th, ja, id, zh), or null for other languages.</summary>
        public static string Spoken(string code)
        {
            switch ((code ?? string.Empty).ToLowerInvariant())
            {
                case "en": return "English";
                case "th": return "ไทย, Thai";
                case "ja": return "日本語, Japanese";
                case "id": return "Bahasa Indonesia";
                case "zh": return "繁體中文, Traditional Chinese";
                default: return null;
            }
        }
    }
}
