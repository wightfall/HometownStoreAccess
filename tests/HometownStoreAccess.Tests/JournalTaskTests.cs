using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>Event journal tasks: the game strikes done tasks through, the mod says "done" (1.3.2).</summary>
    public class JournalTaskTests
    {
        [Fact]
        public void StruckTaskIsDone()
        {
            Assert.True(JournalTasks.IsDone("<s>Go to: Temple</s>"));
            Assert.True(JournalTasks.IsDone("<strikethrough>Money: 271/1000</strikethrough>"));
            Assert.False(JournalTasks.IsDone("Go to: Temple"));
            Assert.False(JournalTasks.IsDone(null));
            Assert.False(JournalTasks.IsDone(string.Empty));
        }

        [Fact]
        public void DoneTaskIsSpokenWithDone()
        {
            Loc.SetLanguage("en");
            try
            {
                Assert.Equal("Go to: Temple, done", JournalTasks.Describe("<s>Go to: Temple</s>", markDone: true));
                Assert.Equal("Go to: Temple", JournalTasks.Describe("<s>Go to: Temple</s>", markDone: false));
                Assert.Equal("Have on shelf: Incense - 0/1", JournalTasks.Describe("Have on shelf: Incense - 0/1", markDone: true));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void ThaiDoneTask()
        {
            Loc.SetLanguage("th");
            try
            {
                Assert.Equal("ไปที่: วัด, เสร็จแล้ว", JournalTasks.Describe("<s>ไปที่: วัด</s>", markDone: true));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void EmptyTaskStaysEmpty()
        {
            Assert.Equal(string.Empty, JournalTasks.Describe("<s></s>", markDone: true));
            Assert.Equal(string.Empty, JournalTasks.Describe(null, markDone: true));
        }
    }
}
