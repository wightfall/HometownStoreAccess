using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>English "N baht" amounts are spoken in the mod's language; thousands separators stay one number (1.4.0).</summary>
    public class BahtAmountTests
    {
        [Fact]
        public void AmountWithThousandsSeparatorIsOneNumber()
        {
            Loc.SetLanguage("th");
            try
            {
                Assert.Equal("Make at least " + Loc.Get("money_baht", 1000), FixedText.Translate("Make at least 1,000 baht"));
                Assert.Equal(Loc.Get("money_baht", 10000), FixedText.Translate("10,000 baht"));
                Assert.Equal("Upgrade: " + Loc.Get("money_baht", 500), FixedText.Translate("Upgrade: 500 baht"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void PlainAmountsStillWork()
        {
            Loc.SetLanguage("en");
            Assert.Equal("12 baht", FixedText.Translate("12 baht"));
            Assert.Equal("-5 baht", FixedText.Translate("-5 baht"));
        }
    }
}
