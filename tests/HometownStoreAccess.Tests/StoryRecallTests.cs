using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class StoryRecallTests
    {
        [Fact]
        public void RemembersTheWholeConversationAndTheLastLine()
        {
            var r = new StoryRecall();
            r.StartConversation();
            r.AddLine("Head: Hello");
            r.AddLine("You: Hi");
            r.AddLine("You: Hi");
            Assert.Equal(new[] { "Head: Hello", "You: Hi" }, r.Conversation);
            Assert.Equal("You: Hi", r.Last);
        }

        [Fact]
        public void ANewConversationReplacesTheOldOneOnlyWhenItSpeaks()
        {
            var r = new StoryRecall();
            r.StartConversation();
            r.AddLine("A: one");
            r.StartConversation();
            Assert.Equal(new[] { "A: one" }, r.Conversation);
            r.AddLine("B: two");
            Assert.Equal(new[] { "B: two" }, r.Conversation);
        }

        [Fact]
        public void HistoryClearForgetsEverything()
        {
            var log = new MessageLog();
            log.Add("Villager came in", MsgCategory.Customers);
            log.Add("Sold candy", MsgCategory.Sales);
            log.CycleCategory(1);
            log.Clear();
            Assert.Equal(MsgCategory.All, log.Current);
            foreach (MsgCategory c in MessageLog.Categories) Assert.Equal(0, log.Count(c));
            Assert.Null(log.Jump(toLast: true));
            log.Add("New day", MsgCategory.Events);
            Assert.Equal("New day", log.Jump(toLast: true));
        }

        [Fact]
        public void KeepsAtMostTheLimit()
        {
            var r = new StoryRecall(limit: 3);
            for (int i = 0; i < 5; i++) r.AddLine("line " + i);
            Assert.Equal(new[] { "line 2", "line 3", "line 4" }, r.Conversation);
            r.Clear();
            Assert.Empty(r.Conversation);
            Assert.Null(r.Last);
        }
    }

    public class FixedTextTests
    {
        [Fact]
        public void TranslatesExactCaptionsAndPatterns()
        {
            try
            {
                Loc.SetLanguage("th");
                Assert.Equal(Loc.Get("fixed_next_day"), FixedText.Translate("Next Day"));
                Assert.Equal(Loc.Get("fixed_day", "3"), FixedText.Translate("Day 3"));
                Assert.Equal(Loc.Get("fixed_storage_shelf", "4", "2"), FixedText.Translate("Storage 4 / Shelf 2"));
                Assert.Equal(Loc.Get("fixed_game_version", "0.7.11A"), FixedText.Translate("Hometown Store Version: 0.7.11A"));
                Assert.Equal(Loc.Get("fixed_version", "0.7.11A"), FixedText.Translate("Version: 0.7.11A"));
                Assert.Equal(Loc.Get("money_baht", 50), FixedText.Translate("50 baht"));
                Assert.Equal("อัพเกรด: " + Loc.Get("money_baht", 500), FixedText.Translate("อัพเกรด: 500 baht"));
                Assert.NotEqual("LOAD GAME", FixedText.Translate("LOAD GAME"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void EveryCaptionIsTranslatedInEveryLanguage()
        {
            foreach (string lang in new[] { "en", "th", "ja", "id", "zh" })
            {
                foreach (string key in FixedText.Keys) Assert.True(Loc.Has(lang, key), lang + " lacks " + key);
            }
        }

        [Fact]
        public void LeavesEverythingElseAlone()
        {
            Loc.SetLanguage("en");
            Assert.Equal("Day trip", FixedText.Translate("Day trip"));
            Assert.Equal("Next Day please", FixedText.Translate("Next Day please"));
            Assert.Equal("ร้านค้า", FixedText.Translate("ร้านค้า"));
            Assert.Equal("", FixedText.Translate(""));
            Assert.Null(FixedText.Translate(null));
        }

        [Fact]
        public void CleanTranslatesWhatTheModReads()
        {
            try
            {
                Loc.SetLanguage("ja");
                Assert.Equal(Loc.Get("fixed_quit_desktop"), TextCleaner.Clean("<b>Quit To OS</b>"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }
    }
}
