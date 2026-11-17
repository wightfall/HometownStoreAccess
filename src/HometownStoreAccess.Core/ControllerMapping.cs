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
    }

    /// <summary>Snapshot of a controller in logical form (after the stick dead zone).</summary>
    public struct PadState
    {
        public PadButtons Buttons;
        public float LeftX, LeftY;
        public byte LeftTrigger, RightTrigger;

        public bool StickUp => LeftY > 0.55f;
        public bool StickDown => LeftY < -0.55f;
        public bool StickLeft => LeftX < -0.55f;
        public bool StickRight => LeftX > 0.55f;
        public bool LeftTriggerDown => LeftTrigger > 128;
        public bool RightTriggerDown => RightTrigger > 128;
    }

    /// <summary>
    /// Converts controller state changes into mod actions. Directional input auto-repeats while held;
    /// all other buttons fire once per press.
    /// </summary>
    public sealed class ControllerMapping
    {
        private readonly KeyRepeater _up = new KeyRepeater();
        private readonly KeyRepeater _down = new KeyRepeater();
        private readonly KeyRepeater _left = new KeyRepeater();
        private readonly KeyRepeater _right = new KeyRepeater();
        private PadState _previous;

        private static readonly (PadButtons button, PadAction action)[] Presses =
        {
            (PadButtons.A, PadAction.Activate),
            (PadButtons.X, PadAction.ActivateAlt),
            (PadButtons.Y, PadAction.Details),
            (PadButtons.B, PadAction.Back),
            (PadButtons.Start, PadAction.Pause),
            (PadButtons.Back, PadAction.Help),
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
            if (state.LeftTriggerDown && !_previous.LeftTriggerDown) actions.Add(PadAction.Repeat);
            if (state.RightTriggerDown && !_previous.RightTriggerDown) actions.Add(PadAction.Status);
            _previous = state;
            return actions;
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
