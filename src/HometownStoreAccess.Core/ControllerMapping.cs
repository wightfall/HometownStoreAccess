using System;
using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>XInput button bits (XINPUT_GAMEPAD.wButtons).</summary>
    [Flags]
    public enum PadButtons : ushort
    {
        None = 0,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
    }

    /// <summary>What a controller press means for the mod.</summary>
    public enum PadAction
    {
        Up,
        Down,
        Left,
        Right,
        Activate,
        ActivateAlt,
        Details,
        Back,
        Pause,
        NextSection,
        PreviousSection,
        Help,
        Repeat,
        Status,
        Customers,
        TogglePause,
        CategoryPrevious,
        CategoryNext,
        HistoryOlder,
        HistoryNewer,
        KeyList,
        ModSettings,
        RecallStory,
    }

    /// <summary>Snapshot of a controller in logical form (after the stick dead zone).</summary>
    public struct PadState
    {
        public PadButtons Buttons;
        public float LeftX, LeftY;
        public float RightX, RightY;
        public byte LeftTrigger, RightTrigger;

        public bool StickUp => LeftY > 0.55f;
        public bool StickDown => LeftY < -0.55f;
        public bool StickLeft => LeftX < -0.55f;
        public bool StickRight => LeftX > 0.55f;
        public bool RightStickUp => RightY > 0.55f && Math.Abs(RightY) >= Math.Abs(RightX);
        public bool RightStickDown => RightY < -0.55f && Math.Abs(RightY) >= Math.Abs(RightX);
        public bool RightStickLeft => RightX < -0.55f && Math.Abs(RightX) > Math.Abs(RightY);
        public bool RightStickRight => RightX > 0.55f && Math.Abs(RightX) > Math.Abs(RightY);
        public bool LeftTriggerDown => LeftTrigger > 128;
        public bool RightTriggerDown => RightTrigger > 128;
    }

    /// <summary>
    /// Converts controller state changes into mod actions. Directional input (and the right stick's
    /// message history) auto-repeats while held; the right stick's category steps and all buttons fire
    /// once per press.
    /// </summary>
    public sealed class ControllerMapping
    {
        private readonly KeyRepeater _up = new KeyRepeater();
        private readonly KeyRepeater _down = new KeyRepeater();
        private readonly KeyRepeater _left = new KeyRepeater();
        private readonly KeyRepeater _right = new KeyRepeater();
        private readonly KeyRepeater _older = new KeyRepeater();
        private readonly KeyRepeater _newer = new KeyRepeater();
        private PadState _previous;

        /// <summary>Holding Back or Start this long (seconds) gives their second action.</summary>
        public const double LongPress = 0.6;

        private double _backDownAt = -1, _startDownAt = -1, _ltDownAt = -1;
        private bool _backLongFired, _startLongFired, _ltLongFired;

        private static readonly (PadButtons button, PadAction action)[] Presses =
        {
            (PadButtons.A, PadAction.Activate),
            (PadButtons.X, PadAction.ActivateAlt),
            (PadButtons.Y, PadAction.Details),
            (PadButtons.B, PadAction.Back),
            (PadButtons.RightShoulder, PadAction.NextSection),
            (PadButtons.LeftShoulder, PadAction.PreviousSection),
            (PadButtons.LeftThumb, PadAction.Customers),
            (PadButtons.RightThumb, PadAction.TogglePause),
        };

        public List<PadAction> Update(PadState state, double now)
        {
            var actions = new List<PadAction>();
            bool up = (state.Buttons & PadButtons.DPadUp) != 0 || state.StickUp;
            bool down = (state.Buttons & PadButtons.DPadDown) != 0 || state.StickDown;
            bool left = (state.Buttons & PadButtons.DPadLeft) != 0 || state.StickLeft;
            bool right = (state.Buttons & PadButtons.DPadRight) != 0 || state.StickRight;
            if (_up.Update(up, now)) actions.Add(PadAction.Up);
            if (_down.Update(down, now)) actions.Add(PadAction.Down);
            if (_left.Update(left, now)) actions.Add(PadAction.Left);
            if (_right.Update(right, now)) actions.Add(PadAction.Right);

            foreach (var (button, action) in Presses)
            {
                bool isDown = (state.Buttons & button) != 0;
                bool wasDown = (_previous.Buttons & button) != 0;
                if (isDown && !wasDown) actions.Add(action);
            }
            // Back: tap for the keys of this screen, hold for the key list. Start: tap for the pause menu,
            // hold for the mod settings. Taps fire on release, holds as soon as the time has passed.
            TapOrHold((state.Buttons & PadButtons.Back) != 0, (_previous.Buttons & PadButtons.Back) != 0, now, ref _backDownAt, ref _backLongFired, PadAction.Help, PadAction.KeyList, actions);
            TapOrHold((state.Buttons & PadButtons.Start) != 0, (_previous.Buttons & PadButtons.Start) != 0, now, ref _startDownAt, ref _startLongFired, PadAction.Pause, PadAction.ModSettings, actions);
            // Left trigger: tap repeats the last message, hold repeats the latest conversation line.
            TapOrHold(state.LeftTriggerDown, _previous.LeftTriggerDown, now, ref _ltDownAt, ref _ltLongFired, PadAction.Repeat, PadAction.RecallStory, actions);
            if (state.RightTriggerDown && !_previous.RightTriggerDown) actions.Add(PadAction.Status);
            // Right stick: up and down step the category, left and right the message history.
            if (state.RightStickUp && !_previous.RightStickUp) actions.Add(PadAction.CategoryPrevious);
            if (state.RightStickDown && !_previous.RightStickDown) actions.Add(PadAction.CategoryNext);
            if (_older.Update(state.RightStickLeft, now)) actions.Add(PadAction.HistoryOlder);
            if (_newer.Update(state.RightStickRight, now)) actions.Add(PadAction.HistoryNewer);
            _previous = state;
            return actions;
        }

        private static void TapOrHold(bool isDown, bool wasDown, double now, ref double downAt, ref bool longFired, PadAction tap, PadAction hold, List<PadAction> actions)
        {
            if (isDown && !wasDown)
            {
                downAt = now;
                longFired = false;
            }
            else if (isDown && !longFired && downAt >= 0 && now - downAt >= LongPress)
            {
                longFired = true;
                actions.Add(hold);
            }
            else if (!isDown && wasDown)
            {
                if (!longFired) actions.Add(tap);
                downAt = -1;
            }
        }

        /// <summary>Applies a radial dead zone and normalises a raw thumbstick axis pair to -1..1.</summary>
        public static void NormalizeStick(short rawX, short rawY, short deadZone, out float x, out float y)
        {
            double fx = rawX, fy = rawY;
            double mag = Math.Sqrt(fx * fx + fy * fy);
            if (mag <= deadZone)
            {
                x = 0f;
                y = 0f;
                return;
            }
            double clipped = Math.Min(mag, 32767.0);
            double scale = (clipped - deadZone) / (32767.0 - deadZone) / mag;
            x = (float)(fx * scale);
            y = (float)(fy * scale);
        }
    }
}
