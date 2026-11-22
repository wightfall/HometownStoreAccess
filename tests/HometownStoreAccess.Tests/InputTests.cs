using System.Collections.Generic;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class KeyRepeaterTests
    {
        [Fact]
        public void FiresOnPressThenRepeatsAfterDelay()
        {
            var r = new KeyRepeater(initialDelay: 0.4, repeatInterval: 0.1);
            Assert.True(r.Update(true, 0.0));
            Assert.False(r.Update(true, 0.2));
            Assert.True(r.Update(true, 0.41));
            Assert.False(r.Update(true, 0.45));
            Assert.True(r.Update(true, 0.52));
            Assert.False(r.Update(false, 0.6));
            Assert.True(r.Update(true, 0.7));
        }
    }

    public class ControllerMappingTests
    {
        private static PadState Buttons(PadButtons b) => new PadState { Buttons = b };

        [Fact]
        public void ButtonsFireOnPressOnly()
        {
            var m = new ControllerMapping();
            List<PadAction> first = m.Update(Buttons(PadButtons.A), 0);
            Assert.Contains(PadAction.Activate, first);
            Assert.DoesNotContain(PadAction.Activate, m.Update(Buttons(PadButtons.A), 0.1));
            m.Update(Buttons(PadButtons.None), 0.2);
            Assert.Contains(PadAction.Activate, m.Update(Buttons(PadButtons.A), 0.3));
        }

        [Fact]
        public void MapsEveryButton()
        {
            var m = new ControllerMapping();
            var all = PadButtons.B | PadButtons.X | PadButtons.Y | PadButtons.LeftShoulder | PadButtons.RightShoulder | PadButtons.LeftThumb | PadButtons.RightThumb;
            List<PadAction> a = m.Update(Buttons(all), 0);
            // B and the left stick click act at once.
            Assert.Contains(PadAction.Back, a);
            Assert.Contains(PadAction.Customers, a);
            // The others act when let go (a short press).
            List<PadAction> released = m.Update(Buttons(PadButtons.None), 0.1);
            Assert.Contains(PadAction.ActivateAlt, released);
            Assert.Contains(PadAction.Details, released);
            Assert.Contains(PadAction.PreviousSection, released);
            Assert.Contains(PadAction.NextSection, released);
            Assert.Contains(PadAction.TogglePause, released);
        }

        [Fact]
        public void AButtonAlreadyHeldWhenTheControllerConnectsIsNotAPress()
        {
            var m = new ControllerMapping();
            m.Seed(Buttons(PadButtons.A | PadButtons.X), 0);
            Assert.Empty(m.Update(Buttons(PadButtons.A | PadButtons.X), 0.1));
            // Let go: nothing (they were not pressed now).
            Assert.Empty(m.Update(Buttons(PadButtons.None), 0.2));
            // A new press acts as usual.
            Assert.Contains(PadAction.Activate, m.Update(Buttons(PadButtons.A), 0.3));
        }

        [Fact]
        public void ADirectionAlreadyHeldWhenTheControllerConnectsDoesNotMoveOrRepeat()
        {
            var m = new ControllerMapping();
            m.Seed(Buttons(PadButtons.DPadDown), 0);
            // Held well past the repeat delay: still nothing.
            for (double t = 0.1; t < 2; t += 0.1) Assert.Empty(m.Update(Buttons(PadButtons.DPadDown), t));
            Assert.Empty(m.Update(Buttons(PadButtons.None), 2.1));
            // Pressed again: it moves.
            Assert.Contains(PadAction.Down, m.Update(Buttons(PadButtons.DPadDown), 2.2));
        }

        [Fact]
        public void AButtonHeldWhenTheControllerDroppedOutDoesNotFireOnReconnect()
        {
            var m = new ControllerMapping();
            m.Update(Buttons(PadButtons.X), 0);
            // The controller drops out with X held; it comes back with X up.
            m.Reset();
            Assert.Empty(m.Update(Buttons(PadButtons.None), 1));
        }

        [Theory]
        [InlineData(PadButtons.X, PadAction.ActivateAlt, PadAction.ActivateThird)]
        [InlineData(PadButtons.Y, PadAction.Details, PadAction.ReadScreen)]
        [InlineData(PadButtons.LeftShoulder, PadAction.PreviousSection, PadAction.FirstItem)]
        [InlineData(PadButtons.RightShoulder, PadAction.NextSection, PadAction.LastItem)]
        [InlineData(PadButtons.RightThumb, PadAction.TogglePause, PadAction.HistoryCategory)]
        public void ButtonsWithASecondActionTapOrHold(PadButtons button, PadAction tap, PadAction hold)
        {
            var m = new ControllerMapping();
            // Tap: on release.
            Assert.Empty(m.Update(Buttons(button), 0));
            Assert.Equal(new[] { tap }, m.Update(Buttons(PadButtons.None), 0.2));
            // Hold: once the time has passed, and nothing on release.
            m.Update(Buttons(button), 1);
            Assert.Equal(new[] { hold }, m.Update(Buttons(button), 1 + ControllerMapping.LongPress + 0.01));
            Assert.Empty(m.Update(Buttons(button), 2.5));
            Assert.Empty(m.Update(Buttons(PadButtons.None), 2.6));
        }

        [Theory]
        [InlineData(PadButtons.Back, PadAction.Help, PadAction.KeyList)]
        [InlineData(PadButtons.Start, PadAction.Pause, PadAction.ModSettings)]
        public void BackAndStartTapOnReleaseAndHoldForMore(PadButtons button, PadAction tap, PadAction hold)
        {
            var m = new ControllerMapping();
            Assert.Empty(m.Update(Buttons(button), 0));
            Assert.Empty(m.Update(Buttons(button), 0.3));
            Assert.Equal(new[] { tap }, m.Update(Buttons(PadButtons.None), 0.4));

            Assert.Empty(m.Update(Buttons(button), 1.0));
            Assert.Equal(new[] { hold }, m.Update(Buttons(button), 1.0 + ControllerMapping.LongPress + 0.01));
            Assert.Empty(m.Update(Buttons(button), 2.0));
            Assert.Empty(m.Update(Buttons(PadButtons.None), 2.1));
        }

        [Fact]
        public void RightStickStepsCategoriesOncePerPush()
        {
            var m = new ControllerMapping();
            Assert.Contains(PadAction.CategoryNext, m.Update(new PadState { RightY = -1f }, 0));
            Assert.DoesNotContain(PadAction.CategoryNext, m.Update(new PadState { RightY = -1f }, 1));
            m.Update(new PadState(), 1.1);
            Assert.Contains(PadAction.CategoryPrevious, m.Update(new PadState { RightY = 1f }, 1.2));
        }

        [Fact]
        public void RightStickSidewaysWalksTheHistoryAndRepeats()
        {
            var m = new ControllerMapping();
            Assert.Contains(PadAction.HistoryOlder, m.Update(new PadState { RightX = -1f }, 0));
            Assert.DoesNotContain(PadAction.HistoryOlder, m.Update(new PadState { RightX = -1f }, 0.2));
            Assert.Contains(PadAction.HistoryOlder, m.Update(new PadState { RightX = -1f }, 0.5));
            m.Update(new PadState(), 0.6);
            Assert.Contains(PadAction.HistoryNewer, m.Update(new PadState { RightX = 1f }, 0.7));
        }

        [Fact]
        public void DiagonalRightStickPicksTheStrongerAxis()
        {
            var m = new ControllerMapping();
            List<PadAction> a = m.Update(new PadState { RightX = 0.6f, RightY = 0.9f }, 0);
            Assert.Contains(PadAction.CategoryPrevious, a);
            Assert.DoesNotContain(PadAction.HistoryNewer, a);
        }

        [Fact]
        public void DpadRepeatsWhileHeld()
        {
            var m = new ControllerMapping();
            Assert.Contains(PadAction.Down, m.Update(Buttons(PadButtons.DPadDown), 0));
            Assert.DoesNotContain(PadAction.Down, m.Update(Buttons(PadButtons.DPadDown), 0.2));
            Assert.Contains(PadAction.Down, m.Update(Buttons(PadButtons.DPadDown), 0.5));
        }

        [Fact]
        public void StickActsLikeDpad()
        {
            var m = new ControllerMapping();
            Assert.Contains(PadAction.Left, m.Update(new PadState { LeftX = -0.9f }, 0));
            Assert.Contains(PadAction.Up, m.Update(new PadState { LeftY = 0.9f }, 1));
        }

        [Fact]
        public void TriggersFireOnce()
        {
            var m = new ControllerMapping();
            // Left trigger: a tap repeats when it is let go.
            Assert.DoesNotContain(PadAction.Repeat, m.Update(new PadState { LeftTrigger = 200 }, 0));
            Assert.DoesNotContain(PadAction.Repeat, m.Update(new PadState { LeftTrigger = 255 }, 0.1));
            Assert.Contains(PadAction.Repeat, m.Update(new PadState(), 0.2));
            // Right trigger: a tap gives the status when it is let go.
            Assert.DoesNotContain(PadAction.Status, m.Update(new PadState { RightTrigger = 200 }, 0.3));
            Assert.Contains(PadAction.Status, m.Update(new PadState(), 0.4));
        }

        [Fact]
        public void HoldingTheRightTriggerGivesTheGoal()
        {
            var m = new ControllerMapping();
            m.Update(new PadState { RightTrigger = 255 }, 0);
            Assert.Contains(PadAction.Goal, m.Update(new PadState { RightTrigger = 255 }, ControllerMapping.LongPress + 0.01));
            Assert.DoesNotContain(PadAction.Goal, m.Update(new PadState { RightTrigger = 255 }, 2));
            Assert.DoesNotContain(PadAction.Status, m.Update(new PadState(), 2.1));
        }

        [Fact]
        public void HoldingTheLeftTriggerRecallsTheConversation()
        {
            var m = new ControllerMapping();
            m.Update(new PadState { LeftTrigger = 255 }, 0);
            Assert.Contains(PadAction.RecallStory, m.Update(new PadState { LeftTrigger = 255 }, ControllerMapping.LongPress + 0.01));
            Assert.DoesNotContain(PadAction.RecallStory, m.Update(new PadState { LeftTrigger = 255 }, 2));
            Assert.DoesNotContain(PadAction.Repeat, m.Update(new PadState(), 2.1));
        }

        [Fact]
        public void NormalizeStick_AppliesDeadZone()
        {
            ControllerMapping.NormalizeStick(3000, 3000, 7849, out float x, out float y);
            Assert.Equal(0f, x);
            Assert.Equal(0f, y);
            ControllerMapping.NormalizeStick(32767, 0, 7849, out x, out y);
            Assert.Equal(1f, x, 3);
            Assert.Equal(0f, y, 3);
            ControllerMapping.NormalizeStick(-32768, 0, 7849, out x, out _);
            Assert.True(x <= -0.99f);
        }
    }

    public class EditEchoTests
    {
        [Fact]
        public void TypedCharacterIsSpoken()
        {
            Assert.Equal("a", EditEcho.DescribeChange("Save", "Savea"));
        }

        [Fact]
        public void SpaceHasAName()
        {
            Loc.SetLanguage("en");
            Assert.Equal("space", EditEcho.DescribeChange("My", "My "));
        }

        [Fact]
        public void DeletedCharacterIsSpoken()
        {
            Loc.SetLanguage("en");
            Assert.Equal("e deleted", EditEcho.DescribeChange("Save", "Sav"));
        }

        [Fact]
        public void OtherChangesSpeakTheWholeText()
        {
            Loc.SetLanguage("en");
            Assert.Equal("QA", EditEcho.DescribeChange("Save Game", "QA"));
            Assert.Equal("empty", EditEcho.DescribeChange("Save", ""));
        }
    }
}
