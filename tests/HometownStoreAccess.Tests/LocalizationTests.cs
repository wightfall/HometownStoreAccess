using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class LocalizationTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}");

        public static IEnumerable<object[]> OtherLanguages()
        {
            foreach (string lang in Loc.SupportedLanguages)
            {
                if (lang != Loc.English) yield return new object[] { lang };
            }
        }

        [Theory]
        [MemberData(nameof(OtherLanguages))]
        public void EveryKeyIsTranslated(string language)
        {
            var missing = Loc.AllKeys.Where(k => !Loc.Has(language, k)).ToList();
            Assert.True(missing.Count == 0, language + " is missing: " + string.Join(", ", missing));
        }

        [Theory]
        [MemberData(nameof(OtherLanguages))]
        public void NoExtraKeysWithoutEnglishSource(string language)
        {
            var extra = Strings.Tables[language].Keys.Where(k => !Loc.Has(Loc.English, k)).ToList();
            Assert.True(extra.Count == 0, language + " has keys English lacks: " + string.Join(", ", extra));
        }

        [Theory]
        [MemberData(nameof(OtherLanguages))]
        public void PlaceholdersMatchEnglish(string language)
        {
            var problems = new List<string>();
            foreach (string key in Loc.AllKeys)
            {
                if (!Loc.Has(language, key)) continue;
                var en = new HashSet<string>(Placeholder.Matches(Loc.Lookup(Loc.English, key)).Cast<Match>().Select(m => m.Value));
                var tr = new HashSet<string>(Placeholder.Matches(Loc.Lookup(language, key)).Cast<Match>().Select(m => m.Value));
                if (!en.SetEquals(tr)) problems.Add(key);
            }
            Assert.True(problems.Count == 0, language + " placeholder mismatch: " + string.Join(", ", problems));
        }

        [Fact]
        public void EnglishValuesAreNotEmpty()
        {
            var empty = Loc.AllKeys.Where(k => string.IsNullOrWhiteSpace(Loc.Lookup(Loc.English, k))).ToList();
            Assert.True(empty.Count == 0, "Empty English strings: " + string.Join(", ", empty));
        }

        [Theory]
        [MemberData(nameof(OtherLanguages))]
        public void TranslatedValuesAreNotEmpty(string language)
        {
            var empty = Strings.Tables[language].Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
            Assert.True(empty.Count == 0, language + " has empty strings: " + string.Join(", ", empty));
        }

        [Fact]
        public void EveryTemplateFormats()
        {
            foreach (string lang in Loc.SupportedLanguages)
            {
                foreach (string key in Loc.AllKeys)
                {
                    string template = Loc.Lookup(lang, key);
                    int count = Placeholder.Matches(template).Count == 0 ? 0 : Placeholder.Matches(template).Cast<Match>().Max(m => int.Parse(m.Groups[1].Value)) + 1;
                    object[] args = Enumerable.Range(0, count).Select(i => (object)("a" + i)).ToArray();
                    string.Format(template, args); // throws FormatException on stray braces
                }
            }
        }

        [Theory]
        [InlineData("en", "en")]
        [InlineData("en-US", "en")]
        [InlineData("th", "th")]
        [InlineData("th-TH", "th")]
        [InlineData("ja", "ja")]
        [InlineData("id", "id")]
        [InlineData("zh-HK", "zh")]
        [InlineData("zh-Hant", "zh")]
        [InlineData("fr", "en")]
        [InlineData(null, "en")]
        public void NormalizeLanguage(string code, string expected)
        {
            Assert.Equal(expected, Loc.NormalizeLanguage(code));
        }

        [Fact]
        public void GetFormatsAndFallsBack()
        {
            Loc.SetLanguage("en");
            Assert.Equal("12 baht", Loc.Get("money_baht", 12));
            Assert.Equal("no_such_key", Loc.Get("no_such_key"));
            Loc.SetLanguage("th");
            Assert.NotEqual("money_baht", Loc.Get("money_baht", 12));
            Loc.SetLanguage("en");
        }
    }
}
