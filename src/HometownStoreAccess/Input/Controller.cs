using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HometownStoreAccess.Core;
using HometownStoreAccess.Game;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess.Input
{
    /// <summary>
    /// XInput controller support, independent of the game's input settings. Controller actions are
    /// translated to the same logical keys the keyboard uses (injected into <see cref="ModInput"/>),
    /// except Back and Start which run the game's Escape behaviour directly.
    /// </summary>
    internal static class Controller
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint dwPacketNumber;
            public XInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputVibration
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint GetState14(uint index, out XInputState state);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        private static extern uint SetState14(uint index, ref XInputVibration vibration);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint GetState910(uint index, out XInputState state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
        private static extern uint SetState910(uint index, ref XInputVibration vibration);

        private const uint ErrorSuccess = 0;
        private const short LeftStickDeadZone = 7849;
        private const short RightStickDeadZone = 8689;

        // Test mode: a simulated pad state fed through the same mapping and dispatch as a real controller.
        private static PadState _simulated;
        private static int _simulatedFrames;
        private static bool _simulatedRelease;

        /// <summary>Holds <paramref name="state"/> for <paramref name="frames"/> frames, then releases everything.</summary>
        public static void Simulate(PadState state, int frames)
        {
            _simulated = state;
            _simulatedFrames = Math.Max(1, frames);
            _simulatedRelease = false;
        }

        private static readonly ControllerMapping Mapping = new ControllerMapping();
        private static bool _useLegacyDll;
        private static bool _disabled;
        private static int _padIndex = -1;
        private static float _nextScan;
        private static float _rumbleUntil;
        private static bool _announcedConnect;

        public static bool Connected => _padIndex >= 0;

        public static void Update()
        {
            if (Plugin.Settings == null || !Plugin.Settings.ControllerEnabled.Value) return;
            if (_simulatedFrames > 0 || _simulatedRelease)
            {
                PadState sim = _simulatedFrames > 0 ? _simulated : default(PadState);
                if (_simulatedFrames > 0)
                {
                    _simulatedFrames--;
                    _simulatedRelease = _simulatedFrames == 0;
                }
                else
                {
                    _simulatedRelease = false;
                }
                foreach (PadAction a in Mapping.Update(sim, Time.unscaledTime)) Dispatch(a);
                return;
            }
            if (_disabled) return;
            try
            {
                if (_padIndex < 0)
                {
                    if (Time.unscaledTime < _nextScan) return;
                    _nextScan = Time.unscaledTime + 2f;
                    for (uint i = 0; i < 4; i++)
                    {
                        if (TryGet(i, out _))
                        {
                            _padIndex = (int)i;
                            if (!_announcedConnect)
                            {
                                _announcedConnect = true;
                                SpeechService.Say(Loc.Get("controller_connected"), false);
                            }
                            break;
                        }
                    }
                    if (_padIndex < 0) return;
                }

                if (!TryGet((uint)_padIndex, out XInputState s))
                {
                    _padIndex = -1;
                    _announcedConnect = false;
                    SpeechService.Say(Loc.Get("controller_disconnected"), false);
                    return;
                }
                if (_rumbleUntil > 0f && Time.unscaledTime > _rumbleUntil) StopRumble();

                var state = new PadState
                {
                    Buttons = (PadButtons)s.Gamepad.wButtons,
                    LeftTrigger = s.Gamepad.bLeftTrigger,
                    RightTrigger = s.Gamepad.bRightTrigger,
                };
                ControllerMapping.NormalizeStick(s.Gamepad.sThumbLX, s.Gamepad.sThumbLY, LeftStickDeadZone, out state.LeftX, out state.LeftY);
                ControllerMapping.NormalizeStick(s.Gamepad.sThumbRX, s.Gamepad.sThumbRY, RightStickDeadZone, out state.RightX, out state.RightY);
                List<PadAction> actions = Mapping.Update(state, Time.unscaledTime);
                foreach (PadAction a in actions) Dispatch(a);
            }
            catch (DllNotFoundException)
            {
                if (!_useLegacyDll)
                {
                    _useLegacyDll = true;
                    return;
                }
                _disabled = true;
                Log.Warn("XInput is not available; controller support disabled.");
            }
            catch (EntryPointNotFoundException)
            {
                _disabled = true;
                Log.Warn("XInput entry points missing; controller support disabled.");
            }
        }

        private static bool TryGet(uint index, out XInputState state)
        {
            uint r = _useLegacyDll ? GetState910(index, out state) : GetState14(index, out state);
            return r == ErrorSuccess;
        }

        private static void Dispatch(PadAction a)
        {
            switch (a)
            {
                case PadAction.Up: ModInput.Inject(KeyCode.UpArrow); break;
                case PadAction.Down: ModInput.Inject(KeyCode.DownArrow); break;
                case PadAction.Left: ModInput.Inject(KeyCode.LeftArrow); break;
                case PadAction.Right: ModInput.Inject(KeyCode.RightArrow); break;
                case PadAction.Activate: ModInput.Inject(KeyCode.Return); break;
                case PadAction.ActivateAlt: ModInput.Inject(KeyCode.Return, shift: true); break;
                case PadAction.Details: ModInput.Inject(KeyCode.I, ctrl: true); break;
                case PadAction.NextSection: ModInput.Inject(KeyCode.Tab); break;
                case PadAction.PreviousSection: ModInput.Inject(KeyCode.Tab, shift: true); break;
                case PadAction.Help: ModInput.Inject(KeyCode.F1); break;
                case PadAction.Repeat: ModInput.Inject(KeyCode.R, ctrl: true); break;
                case PadAction.Status: ModInput.Inject(KeyCode.T, ctrl: true); break;
                case PadAction.Customers: ModInput.Inject(KeyCode.K, ctrl: true); break;
                case PadAction.TogglePause: ModInput.Inject(KeyCode.P, ctrl: true); break;
                case PadAction.CategoryPrevious: ModInput.Inject(KeyCode.UpArrow, ctrl: true); break;
                case PadAction.CategoryNext: ModInput.Inject(KeyCode.DownArrow, ctrl: true); break;
                case PadAction.HistoryOlder: ModInput.Inject(KeyCode.Comma); break;
                case PadAction.HistoryNewer: ModInput.Inject(KeyCode.Period); break;
                case PadAction.KeyList: ModInput.Inject(KeyCode.F1, shift: true); break;
                case PadAction.ModSettings: ModInput.Inject(KeyCode.F5); break;
                case PadAction.RecallStory: ModInput.Inject(KeyCode.Backslash); break;
                case PadAction.Back: GameActions.Back(); break;
                case PadAction.Pause:
                    if (Contexts.Overlay.IsOpen) Contexts.Overlay.Close();
                    else if (GlobeOption.Instance != null) GlobeOption.Instance.ToggleOptionPanel();
                    break;
            }
        }

        /// <summary>Short vibration (errors, blocked placement).</summary>
        public static void Rumble(float strength, float seconds)
        {
            if (_padIndex < 0 || !Plugin.Settings.ControllerRumble.Value) return;
            try
            {
                ushort v = (ushort)(Mathf.Clamp01(strength) * 65535f);
                var vib = new XInputVibration { wLeftMotorSpeed = v, wRightMotorSpeed = v };
                if (_useLegacyDll) SetState910((uint)_padIndex, ref vib); else SetState14((uint)_padIndex, ref vib);
                _rumbleUntil = Time.unscaledTime + seconds;
            }
            catch
            {
                // Rumble is optional.
            }
        }

        private static void StopRumble()
        {
            _rumbleUntil = 0f;
            try
            {
                var vib = new XInputVibration();
                if (_useLegacyDll) SetState910((uint)_padIndex, ref vib); else SetState14((uint)_padIndex, ref vib);
            }
            catch
            {
                // ignored
            }
        }
    }
}
