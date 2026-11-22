using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>English texts written into the game's code are spoken in the mod's language (1.4.1).</summary>
    public class HardCodedEnglishTests
    {
        [Fact]
        public void NoStorageSpaceNamesTheItem()
        {
            var before = FixedText.AssetName;
            Loc.SetLanguage("th");
            try
            {
                FixedText.AssetName = name => name == "Candy" ? "ลูกอม" : name;
                Assert.Equal("พื้นที่เก็บของไม่พอสำหรับ ลูกอม", FixedText.Translate("Not Enough Storage Space for Candy (Item)"));
                Assert.Equal("พื้นที่เก็บของไม่พอสำหรับ ลูกอม", FixedText.Translate("Not Enough Storage Space for Candy"));
            }
            finally
            {
                FixedText.AssetName = before;
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void BahtSignAmountsAreSpokenAsMoney()
        {
            Loc.SetLanguage("en");
            Assert.Equal("+18 baht", FixedText.Translate("+18฿"));
            Assert.Equal("-11 baht", FixedText.Translate("-11฿"));
            Assert.Equal("236 baht", FixedText.Translate("236 ฿"));
            Assert.Equal("1000 baht", FixedText.Translate("1,000฿"));
            Loc.SetLanguage("th");
            try
            {
                Assert.Equal("เงิน 258 บาท", FixedText.Translate("เงิน 258฿"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void NoticeButtonAndCreditsAreTranslated()
        {
            Loc.SetLanguage("th");
            try
            {
                Assert.Equal("ตกลง", FixedText.Translate("Ok"));
                Assert.Equal("Yotsaphat Lapharotkit: ผู้กำกับ, นักออกแบบเกม, โปรแกรมเมอร์", FixedText.Translate("Yotsaphat Lapharotkit [Director, Game Designer, Programmer]"));
                Assert.Equal("Thanajan Saesum: เทคนิคอาร์ต", FixedText.Translate("Thanajan Saesum [Tech-Art]"));
                // A role the mod does not know keeps the whole line as the game shows it.
                Assert.Equal("Someone [Composer]", FixedText.Translate("Someone [Composer]"));
                Assert.NotEqual("And to all supporters who believed in the journey.", FixedText.Translate("And to all supporters who believed in the journey."));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void BothSpellingsOfAreYouSureAreTranslated()
        {
            Loc.SetLanguage("ja");
            try
            {
                Assert.Equal(Loc.Get("fixed_are_you_sure"), FixedText.Translate("Are you sure about that?"));
                Assert.Equal(Loc.Get("fixed_are_you_sure"), FixedText.Translate("Are You Sure About That?"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }
    }
}
