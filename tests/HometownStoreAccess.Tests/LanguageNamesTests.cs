using System.Linq;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class LanguageNamesTests
    {
        [Fact]
        public void EveryBuiltInLanguageHasASpokenName()
        {
            foreach (string code in Loc.SupportedLanguages)
            {
                Assert.False(string.IsNullOrEmpty(LanguageNames.Spoken(code)), code);
            }
        }

        [Fact]
        public void EveryNameCanBeReadInLatinLetters()
        {
            // Any voice must be able to say which language was picked: a name in Thai, Japanese or
            // Chinese letters also carries the English name.
            foreach (string code in Loc.SupportedLanguages)
            {
                string name = LanguageNames.Spoken(code);
                bool latinWord = name.Split(',', ' ').Any(w => w.Length > 2 && w.All(c => c >= 'A' && c <= 'z'));
                Assert.True(latinWord, code + ": " + name);
            }
        }

        [Fact]
        public void NamesStartInTheirOwnLanguage()
        {
            Assert.StartsWith("ไทย", LanguageNames.Spoken("th"));
            Assert.StartsWith("日本語", LanguageNames.Spoken("JA"));
            Assert.StartsWith("繁體中文", LanguageNames.Spoken("zh"));
            Assert.Equal("English", LanguageNames.Spoken("en"));
            Assert.Null(LanguageNames.Spoken("es"));
            Assert.Null(LanguageNames.Spoken(null));
        }
    }
}
