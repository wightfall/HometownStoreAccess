using System.Collections.Generic;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class RewardTextTests
    {
        [Fact]
        public void TranslatesTheFixedEnglishCaptions()
        {
            Loc.SetLanguage("en");
            Assert.Equal("New customer", RewardText.Translate("You Unlock"));
            Assert.Equal("You got", RewardText.Translate("You Get"));
            Assert.Equal("Popularity up!", RewardText.Translate("Popularity Up!"));
            Assert.Equal("Level 3", RewardText.Translate("Level 3"));
        }

        [Fact]
        public void SpeaksCaptionsAndMoneyInTheModLanguage()
        {
            try
            {
                Loc.SetLanguage("ja");
                Assert.Equal(Loc.Get("reward_new_customer"), RewardText.Translate("You Unlock"));
                Assert.NotEqual("You Unlock", RewardText.Translate("You Unlock"));
                Assert.Equal(Loc.Get("money_baht", 50), RewardText.Translate("50 baht"));
                Assert.Equal(Loc.Get("reward_level", "2"), RewardText.Translate("Level 2"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void LeavesOtherTextsAlone()
        {
            Loc.SetLanguage("en");
            Assert.Equal("おめでとう!!", RewardText.Translate("おめでとう!!"));
            Assert.Equal("Candy x3", RewardText.Translate("Candy x3"));
            Assert.Null(RewardText.Translate(null));
        }

        [Fact]
        public void DropsPlaceholdersAndCountsTheNameAlreadySays()
        {
            Loc.SetLanguage("en");
            List<string> spoken = RewardText.Prepare(new[] { "Congratulations!!", "999", "3", "You Get", "Candy x3", "x999", "", "12" });
            Assert.Equal(new[] { "Congratulations!!", "You got", "Candy x3", "12" }, spoken);
            // The reward screen's item line is said as a stack.
            Assert.Equal("Candy: 3", RewardText.Quantity("Candy x3"));
            Assert.Equal("Level 3", RewardText.Quantity("Level 3"));
        }

        [Fact]
        public void ReadsOnlyTheBudgetLimitsThatChange()
        {
            Loc.SetLanguage("en");
            Assert.Equal("Lowest budget 2 to 3 baht. Highest budget 5 to 7 baht", RewardText.Budget(true, "2", "3", true, "5", "7"));
            Assert.Equal("Highest budget 5 to 7 baht", RewardText.Budget(false, "[Base]", "[New]", true, "5", "7"));
            Assert.Equal("Lowest budget 2 to 3 baht", RewardText.Budget(true, "2", "3", false, "[Base]", "[New]"));
            Assert.Null(RewardText.Budget(false, "", "", false, "", ""));
        }
    }
}
