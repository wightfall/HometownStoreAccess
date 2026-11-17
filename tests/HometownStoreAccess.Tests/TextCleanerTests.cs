using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class TextCleanerTests
    {
        [Theory]
        [InlineData("<b>Candy</b>", "Candy")]
        [InlineData("<color=#ff0000>Red</color> text", "Red text")]
        [InlineData("<size=120%>Big</size>", "Big")]
        [InlineData("a<br>b", "a b")]
        [InlineData("  many   spaces \n here ", "many spaces here")]
        [InlineData("<sprite=3> icon", "icon")]
        [InlineData("<s>-Shelf</s>", "-Shelf")]
        public void Clean_RemovesRichTextAndNormalisesWhitespace(string raw, string expected)
        {
            Assert.Equal(expected, TextCleaner.Clean(raw));
        }

        [Fact]
        public void Clean_KeepsAngleBracketsThatAreNotTags()
        {
            Assert.Equal("Profit < 0 baht", TextCleaner.Clean("Profit < 0 baht"));
        }

        [Fact]
        public void Clean_ConvertsLiteralNewlineEscapes()
        {
            Assert.Equal("first line second line", TextCleaner.Clean("first line\\n second line"));
        }

        [Theory]
        [InlineData("No translation found for 'Exit' in MainMenu", "Exit")]
        [InlineData("Key 'Phase Morning' not found in table 'MainInterface'", "Phase Morning")]
        public void Clean_ReplacesMissingTranslationPlaceholdersWithTheKey(string raw, string expected)
        {
            Assert.Equal(expected, TextCleaner.Clean(raw));
        }

        [Fact]
        public void Clean_NullIsEmpty()
        {
            Assert.Equal(string.Empty, TextCleaner.Clean(null));
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("---", true)]
        [InlineData("x", false)]
        [InlineData("๑", false)]
        public void IsBlank(string s, bool blank)
        {
            Assert.Equal(blank, TextCleaner.IsBlank(s));
        }

        [Fact]
        public void Join_SkipsBlanksAndRepeats()
        {
            Assert.Equal("Shelf, Candy", TextCleaner.Join(", ", "Shelf", "", null, "shelf", "Candy"));
        }

        [Fact]
        public void Join_DoesNotDoublePunctuation()
        {
            Assert.Equal("Dear grandchild, I have news", TextCleaner.Join(", ", "Dear grandchild,", "I have news"));
            Assert.Equal("Nothing bought today. 2 of 7", TextCleaner.Join(", ", "Nothing bought today.", "2 of 7"));
        }

        [Theory]
        [InlineData("CloseButton", "close")]
        [InlineData("Btn_NextPhase (1)", "next phase")]
        [InlineData("TC_x2", "tc x 2")]
        [InlineData("nextPhaseButton", "next phase")]
        [InlineData("Load Game", "load game")]
        public void HumanizeName(string name, string expected)
        {
            Assert.Equal(expected, TextCleaner.HumanizeName(name));
        }

        [Theory]
        [InlineData("Done.", true)]
        [InlineData("Done", false)]
        [InlineData("เสร็จแล้ว!", true)]
        [InlineData("終わり。", true)]
        [InlineData("", false)]
        public void EndsWithPunctuation(string s, bool expected)
        {
            Assert.Equal(expected, TextCleaner.EndsWithPunctuation(s));
        }
    }
}
