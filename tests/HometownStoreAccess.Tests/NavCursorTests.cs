using System.Collections.Generic;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class NavCursorTests
    {
        private static List<string> Keys(params string[] k) => new List<string>(k);

        [Fact]
        public void Reset_StartsAtFirstItem()
        {
            var c = new NavCursor();
            c.Reset(Keys("a", "b", "c"));
            Assert.Equal(0, c.Index);
            Assert.Equal("a", c.FocusedKey);
        }

        [Fact]
        public void Reset_FollowsFocusedKeyWhenListIsRebuilt()
        {
            var c = new NavCursor();
            var keys = Keys("a", "b", "c");
            c.Reset(keys);
            c.Move(2, keys);
            bool changed = c.Reset(Keys("x", "c", "a"));
            Assert.False(changed);
            Assert.Equal(1, c.Index);
            Assert.Equal("c", c.FocusedKey);
        }

        [Fact]
        public void Reset_KeepsIndexWhenFocusedItemDisappears()
        {
            var c = new NavCursor();
            var keys = Keys("a", "b", "c");
            c.Reset(keys);
            c.Move(1, keys);
            bool changed = c.Reset(Keys("a", "c"));
            Assert.True(changed);
            Assert.Equal(1, c.Index);
            Assert.Equal("c", c.FocusedKey);
        }

        [Fact]
        public void Reset_ClampsToShorterList()
        {
            var c = new NavCursor();
            var keys = Keys("a", "b", "c");
            c.Reset(keys);
            c.MoveTo(2, keys);
            c.Reset(Keys("q"));
            Assert.Equal(0, c.Index);
        }

        [Fact]
        public void Reset_EmptyList()
        {
            var c = new NavCursor();
            c.Reset(Keys("a"));
            Assert.True(c.Reset(Keys()));
            Assert.Equal(-1, c.Index);
            Assert.Equal(MoveResult.Empty, c.Move(1, Keys()));
        }

        [Fact]
        public void Move_WrapsAround()
        {
            var c = new NavCursor();
            var keys = Keys("a", "b");
            c.Reset(keys);
            Assert.Equal(MoveResult.Moved, c.Move(1, keys));
            Assert.Equal(MoveResult.Wrapped, c.Move(1, keys));
            Assert.Equal("a", c.FocusedKey);
            Assert.Equal(MoveResult.Wrapped, c.Move(-1, keys));
            Assert.Equal("b", c.FocusedKey);
        }

        [Fact]
        public void Move_StopsAtEdgeWithoutWrap()
        {
            var c = new NavCursor { Wrap = false };
            var keys = Keys("a", "b");
            c.Reset(keys);
            Assert.Equal(MoveResult.AtEdge, c.Move(-1, keys));
            Assert.Equal(0, c.Index);
        }

        [Fact]
        public void Move_SingleItemIsEdge()
        {
            var c = new NavCursor();
            var keys = Keys("only");
            c.Reset(keys);
            Assert.Equal(MoveResult.AtEdge, c.Move(1, keys));
        }

        [Fact]
        public void MoveGroup_JumpsBetweenGroupStarts()
        {
            var c = new NavCursor();
            var keys = Keys("s1", "s2", "t1", "t2", "a1");
            var groups = Keys("Shelf", "Shelf", "Storage", "Storage", "Actions");
            c.Reset(keys);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(1, groups, keys));
            Assert.Equal("t1", c.FocusedKey);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(1, groups, keys));
            Assert.Equal("a1", c.FocusedKey);
            Assert.Equal(MoveResult.Wrapped, c.MoveGroup(1, groups, keys));
            Assert.Equal("s1", c.FocusedKey);
        }

        [Fact]
        public void MoveGroup_BackwardsGoesToGroupStartThenPreviousGroup()
        {
            var c = new NavCursor();
            var keys = Keys("s1", "s2", "t1", "t2");
            var groups = Keys("Shelf", "Shelf", "Storage", "Storage");
            c.Reset(keys);
            c.MoveTo(3, keys);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(-1, groups, keys));
            Assert.Equal("t1", c.FocusedKey);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(-1, groups, keys));
            Assert.Equal("s1", c.FocusedKey);
            Assert.Equal(MoveResult.Wrapped, c.MoveGroup(-1, groups, keys));
            Assert.Equal("t1", c.FocusedKey);
        }

        [Fact]
        public void MoveGroup_SingleGroupIsEdge()
        {
            var c = new NavCursor();
            var keys = Keys("a", "b");
            var groups = Keys("", "");
            c.Reset(keys);
            Assert.Equal(MoveResult.AtEdge, c.MoveGroup(1, groups, keys));
        }

        [Fact]
        public void MoveGroup_WithoutWrapStopsAtFirstAndLastGroup()
        {
            var c = new NavCursor { Wrap = false };
            var keys = Keys("s1", "s2", "t1", "a1");
            var groups = Keys("Shelf", "Shelf", "Storage", "Actions");
            c.Reset(keys);
            Assert.Equal(MoveResult.AtEdge, c.MoveGroup(-1, groups, keys));
            Assert.Equal("s1", c.FocusedKey);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(1, groups, keys));
            Assert.Equal(MoveResult.Moved, c.MoveGroup(1, groups, keys));
            Assert.Equal("a1", c.FocusedKey);
            Assert.Equal(MoveResult.AtEdge, c.MoveGroup(1, groups, keys));
            Assert.Equal("a1", c.FocusedKey);
            // Backwards from inside a group still goes to that group's start first.
            c.MoveTo(1, keys);
            Assert.Equal(MoveResult.Moved, c.MoveGroup(-1, groups, keys));
            Assert.Equal("s1", c.FocusedKey);
        }

        [Fact]
        public void Move_WithoutWrapStaysOnLastItem()
        {
            var c = new NavCursor { Wrap = false };
            var keys = Keys("a", "b", "c");
            c.Reset(keys);
            c.MoveTo(2, keys);
            Assert.Equal(MoveResult.AtEdge, c.Move(1, keys));
            Assert.Equal("c", c.FocusedKey);
            c.Wrap = true;
            Assert.Equal(MoveResult.Wrapped, c.Move(1, keys));
            Assert.Equal("a", c.FocusedKey);
        }

        [Fact]
        public void FindNextByPrefix_WrapsAndIgnoresCase()
        {
            var c = new NavCursor();
            var keys = Keys("1", "2", "3");
            var labels = Keys("Candy", "Gum", "chips");
            c.Reset(keys);
            Assert.True(c.FindNextByPrefix("c", labels, keys));
            Assert.Equal("3", c.FocusedKey);
            Assert.True(c.FindNextByPrefix("C", labels, keys));
            Assert.Equal("1", c.FocusedKey);
            Assert.False(c.FindNextByPrefix("z", labels, keys));
        }
    }
}
