using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class ChoiceStepTests
    {
        [Fact]
        public void StepsWithinTheList()
        {
            Assert.Equal(2, ChoiceStep.Step(1, 5, 1, wrap: true, out bool edge));
            Assert.False(edge);
            Assert.Equal(0, ChoiceStep.Step(1, 5, -1, wrap: false, out edge));
            Assert.False(edge);
        }

        [Fact]
        public void WrapsRoundBothEnds()
        {
            Assert.Equal(0, ChoiceStep.Step(4, 5, 1, wrap: true, out bool edge));
            Assert.False(edge);
            Assert.Equal(4, ChoiceStep.Step(0, 5, -1, wrap: true, out edge));
            Assert.False(edge);
        }

        [Fact]
        public void WithoutWrapStaysAtTheEnds()
        {
            Assert.Equal(4, ChoiceStep.Step(4, 5, 1, wrap: false, out bool edge));
            Assert.True(edge);
            Assert.Equal(0, ChoiceStep.Step(0, 5, -1, wrap: false, out edge));
            Assert.True(edge);
        }

        [Fact]
        public void UnknownValueStartsAtTheFirst()
        {
            Assert.Equal(0, ChoiceStep.Step(-1, 3, 1, wrap: false, out bool edge));
            Assert.False(edge);
            Assert.Equal(0, ChoiceStep.Step(7, 3, -1, wrap: true, out edge));
            Assert.False(edge);
        }

        [Fact]
        public void SingleAndEmptyLists()
        {
            Assert.Equal(0, ChoiceStep.Step(0, 1, 1, wrap: true, out bool edge));
            Assert.True(edge);
            Assert.Equal(-1, ChoiceStep.Step(0, 0, 1, wrap: true, out edge));
            Assert.True(edge);
        }

        [Fact]
        public void LargeDirectionStillMovesOneStep()
        {
            Assert.Equal(2, ChoiceStep.Step(1, 5, 10, wrap: true, out _));
            Assert.Equal(0, ChoiceStep.Step(1, 5, -3, wrap: true, out _));
        }

        [Fact]
        public void GroupCountCountsRunsOfTheSameGroup()
        {
            Assert.Equal(0, ChoiceStep.GroupCount(new string[0]));
            Assert.Equal(1, ChoiceStep.GroupCount(new[] { "", "", "" }));
            Assert.Equal(1, ChoiceStep.GroupCount(new[] { null, "" }));
            Assert.Equal(3, ChoiceStep.GroupCount(new[] { "", "", "General", "General", "Sound" }));
            // A name used again later is a new stop for Page Down.
            Assert.Equal(3, ChoiceStep.GroupCount(new[] { "A", "B", "A" }));
        }
    }
}
