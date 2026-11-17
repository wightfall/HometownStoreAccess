using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class SpeechBufferTests
    {
        [Fact]
        public void Flush_CombinesFrameIntoOneUtterance()
        {
            var b = new SpeechBuffer();
            b.Add("Shelf panel", true);
            b.Add("Candy: 5", false);
            Utterance? u = b.Flush(1.0);
            Assert.True(u.HasValue);
            Assert.Equal("Shelf panel. Candy: 5", u.Value.Text);
            Assert.True(u.Value.Interrupt);
        }

        [Fact]
        public void Flush_KeepsExistingPunctuation()
        {
            var b = new SpeechBuffer();
            b.Add("Saving the game.", false);
            b.Add("The shop is open.", false);
            Assert.Equal("Saving the game. The shop is open.", b.Flush(1.0).Value.Text);
        }

        [Fact]
        public void Flush_NonInterruptingWhenNothingInterrupts()
        {
            var b = new SpeechBuffer();
            b.Add("A customer came in.", false);
            Assert.False(b.Flush(1.0).Value.Interrupt);
        }

        [Fact]
        public void Flush_EmptyReturnsNull()
        {
            Assert.Null(new SpeechBuffer().Flush(0));
        }

        [Fact]
        public void Add_IgnoresBlankAndImmediateDuplicate()
        {
            var b = new SpeechBuffer();
            b.Add("", true);
            b.Add("<b></b>", true);
            b.Add("Hello", true);
            b.Add("Hello", true);
            Assert.Equal("Hello", b.Flush(0).Value.Text);
        }

        [Fact]
        public void Flush_SuppressesSameUtteranceWithinRepeatWindow()
        {
            var b = new SpeechBuffer(repeatWindowSeconds: 0.25);
            b.Add("Edge", true);
            Assert.NotNull(b.Flush(10.0));
            b.Add("Edge", true);
            Assert.Null(b.Flush(10.1));
            b.Add("Edge", true);
            Assert.NotNull(b.Flush(10.5));
        }

        [Fact]
        public void History_NewestFirstAndBounded()
        {
            var b = new SpeechBuffer(historyLimit: 2);
            foreach (string s in new[] { "one", "two", "three" })
            {
                b.Add(s, true);
                b.Flush(0);
            }
            Assert.Equal(2, b.History.Count);
            Assert.Equal("three", b.HistoryAt(0));
            Assert.Equal("two", b.HistoryAt(1));
            Assert.Null(b.HistoryAt(2));
            Assert.Equal("three", b.LastSpoken);
        }

        [Fact]
        public void ClearPending_DropsQueuedText()
        {
            var b = new SpeechBuffer();
            b.Add("old", true);
            b.ClearPending();
            Assert.False(b.HasPending);
            Assert.Null(b.Flush(0));
        }
    }
}
