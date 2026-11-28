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

        private static bool IsHeading(string key) => key.StartsWith("heading:");

        [Fact]
        public void AVanishedItemHandsTheFocusToItsNeighbourInTheSameSection()
        {
            // A shelf opened with the storage: the storage's Candy moves whole into a new shelf slot.
            var oldKeys = new List<string> { "cat", "heading:Shelf", "t:A", "lock:A", "t:B", "lock:B", "heading:Storage", "s:Candy", "s:D" };
            var oldGroups = new List<string> { "", "Shelf", "Shelf", "Shelf", "Shelf", "Shelf", "Storage", "Storage", "Storage" };
            var newKeys = new List<string> { "cat", "heading:Shelf", "t:A", "lock:A", "t:B", "lock:B", "t:Candy", "lock:Candy", "heading:Storage", "s:D" };
            // Not the shelf's new lock check box that now sits at the old position: the next storage stack.
            Assert.Equal(newKeys.IndexOf("s:D"), NavCursor.NearestSurvivor(oldKeys, oldGroups, 7, newKeys, IsHeading));
        }

        [Fact]
        public void TheFocusStaysInItsSectionWhenTheLastEntryGoes()
        {
            // Unlocking the shelf's last, empty slot removes it (and its lock): back to the slot before it.
            var oldKeys = new List<string> { "heading:Shelf", "t:A", "lock:A", "t:B", "lock:B", "heading:Storage", "s:C" };
            var oldGroups = new List<string> { "Shelf", "Shelf", "Shelf", "Shelf", "Shelf", "Storage", "Storage" };
            var newKeys = new List<string> { "heading:Shelf", "t:A", "lock:A", "heading:Storage", "s:C" };
            Assert.Equal(newKeys.IndexOf("lock:A"), NavCursor.NearestSurvivor(oldKeys, oldGroups, 4, newKeys, IsHeading));
        }

        [Fact]
        public void ASectionThatEmptiesHandsTheFocusToTheNearestEntryElsewhere()
        {
            // The basket's only line is removed: the nearest entry left (never a heading).
            var oldKeys = new List<string> { "heading:Goods", "g:Candy", "heading:Basket", "b:Candy", "heading:Actions", "checkout" };
            var oldGroups = new List<string> { "Goods", "Goods", "Basket", "Basket", "Actions", "Actions" };
            var newKeys = new List<string> { "heading:Goods", "g:Candy", "heading:Actions", "checkout" };
            Assert.Equal(newKeys.IndexOf("checkout"), NavCursor.NearestSurvivor(oldKeys, oldGroups, 3, newKeys, IsHeading));
            // Nothing left at all, or no old focus: no answer (the cursor keeps its position).
            Assert.Equal(-1, NavCursor.NearestSurvivor(oldKeys, oldGroups, 3, new List<string> { "heading:Goods" }, IsHeading));
            Assert.Equal(-1, NavCursor.NearestSurvivor(oldKeys, oldGroups, -1, newKeys, IsHeading));
        }

        [Fact]
        public void ASectionIsItsOwnRunNotEveryItemWithTheSameName()
        {
            // Unnamed entries at the top and at the bottom are two sections: when the top one empties,
            // the focus does not jump to the bottom's Close because both have no name.
            var oldKeys = new List<string> { "emp:a", "heading:Pages", "tab:1", "tab:2", "close" };
            var oldGroups = new List<string> { "", "Pages", "Pages", "Pages", "" };
            var newKeys = new List<string> { "emp:b", "heading:Pages", "tab:1", "tab:2", "close" };
            // Nothing of its own run is left: the nearest entry anywhere, the first tab (not Close).
            Assert.Equal(newKeys.IndexOf("tab:1"), NavCursor.NearestSurvivor(oldKeys, oldGroups, 0, newKeys, IsHeading));
        }

        [Fact]
        public void HeadingsOnlyWhenPageDownHasSomewhereToGo()
        {
            // 1.5.0: every list with sections gets headings, not only the key list and settings.
            // The main menu: the game buttons without a name, then "More".
            Assert.True(ChoiceStep.WantsHeadings(new List<string> { "", "", "", "More", "More" }));
            Assert.True(ChoiceStep.WantsHeadings(new List<string> { "Places", "Places", "Map" }));
            // One section only: its name stays in front of the item, no heading.
            Assert.False(ChoiceStep.WantsHeadings(new List<string> { "Saves", "Saves" }));
            // No names at all (credits, a notice): nothing to head.
            Assert.False(ChoiceStep.WantsHeadings(new List<string> { "", null, "" }));
            Assert.False(ChoiceStep.WantsHeadings(new List<string>()));
            Assert.False(ChoiceStep.WantsHeadings(null));
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
                foreach (string key in new[] { "role_heading", "heading_items", "journal_untitled", "journal_untitled_at", "journal_untitled_of", "journal_untitled_level" })
                {
                    Assert.True(Loc.Has(lang, key), lang + " " + key);
                }
            }
        }
    }
}
