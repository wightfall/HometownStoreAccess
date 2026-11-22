using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>The day summary names an upkeep bill "&lt;building&gt;Upkeep"; it is spoken in the mod's language (1.4.1).</summary>
    public class UpkeepBillTests
    {
        [Fact]
        public void UpkeepBillsAreSpokenInTheModLanguage()
        {
            Loc.SetLanguage("th");
            try
            {
                Assert.Equal("ค่าดูแล ถังแช่", FixedText.Translate("ถังแช่Upkeep"));
                Loc.SetLanguage("en");
                Assert.Equal("Cooler upkeep", FixedText.Translate("CoolerUpkeep"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void OtherTextsAreLeftAlone()
        {
            Loc.SetLanguage("en");
            Assert.Equal("Upkeep", FixedText.Translate("Upkeep"));
            Assert.Equal("Upkeep: 5 baht per day", FixedText.Translate("Upkeep: 5 baht per day"));
        }
    }
}
