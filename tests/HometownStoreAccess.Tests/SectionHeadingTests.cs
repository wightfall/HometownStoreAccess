using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>Heading items in the key list and the mod settings (1.3.2).</summary>
    public class SectionHeadingTests
    {
        [Fact]
        public void EachNamedSectionStartsOnceWithItsSize()
        {
            var groups = new List<string> { "Speech", "Speech", "Audio", "Actions", "Actions", "Actions" };
            List<KeyValuePair<int, int>> starts = ChoiceStep.SectionStarts(groups);
            Assert.Equal(new[] { 0, 2, 3 }, starts.Select(s => s.Key).ToArray());
            Assert.Equal(new[] { 2, 1, 3 }, starts.Select(s => s.Value).ToArray());
        }

        [Fact]
        public void ItemsWithoutASectionGetNoHeading()
        {
            var groups = new List<string> { "", null, "Keys", "Keys", "" };
            List<KeyValuePair<int, int>> starts = ChoiceStep.SectionStarts(groups);
            Assert.Single(starts);
            Assert.Equal(2, starts[0].Key);
            Assert.Equal(2, starts[0].Value);
        }

        [Fact]
        public void EmptyListHasNoSections()
        {
            Assert.Empty(ChoiceStep.SectionStarts(new List<string>()));
            Assert.Empty(ChoiceStep.SectionStarts(null));
        }

        [Fact]
        public void PageDownLandsOnTheHeadingOfTheNextSection()
        {
            // The list as the key list builds it: a heading item first in every section.
            var groups = new List<string> { "General", "General", "General", "Actions", "Actions", "Actions" };
            var keys = new List<string> { "heading:General", "k0", "k1", "heading:Actions", "open_settings", "close" };
            var c = new NavCursor { Wrap = true };
            c.Reset(keys);
            c.Move(1, keys);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(1, groups, keys));
            Assert.Equal("heading:Actions", c.FocusedKey);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(-1, groups, keys));
            Assert.Equal("heading:General", c.FocusedKey);
        }

        [Fact]
        public void HeadingStringsExistInEveryLanguage()
        {
            // (Placeholders are checked against English for every key by the localization tests.)
            foreach (string lang in Loc.SupportedLanguages)
            {
                foreach (string key in new[] { "role_heading", "heading_items", "journal_untitled", "journal_untitled_at", "map_nothing_here" })
                {
                    Assert.True(Loc.Has(lang, key), lang + " " + key);
                }
            }
        }
    }
}
