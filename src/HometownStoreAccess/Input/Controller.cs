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
        private static volatile bool _useLegacyDll;
        private static bool _disabled;
        private static int _padIndex = -1;
        private static uint _scanSlot;
        private static bool _seedNext;
        private static bool _enabled = true;
        private static float _nextScan;
        private static float _rumbleUntil;
        private static bool _announcedConnect;

        public static bool Connected => _padIndex >= 0;

        /// <summary>
        /// Plugging a controller in makes instructions name its buttons (until a keyboard key is used);
        /// unplugging it brings back the keyboard keys. Both are said. (Also the test command "pad connect".)
        /// </summary>
        public static void ConnectionChanged(bool connected)
        {
            Loc.PadMode = connected;
            SpeechService.Say(Loc.Get(connected ? "controller_connected" : "controller_disconnected"), false);
        }

        /// <summary>
        /// The Controller setting was turned off: let go of the controller. Instructions name the keyboard keys
        /// again, and nothing held now acts when it is turned back on (the controller then connects afresh).
        /// </summary>
        private static void TurnedOff()
        {
            _enabled = false;
            if (_padIndex >= 0) StopRumble();
            _padIndex = -1;
            _probeFound = -1;
            _announcedConnect = false;
            _seedNext = false;
            Mapping.Reset();
            Loc.PadMode = false;
        }

        public static void Update()
        {
            if (Plugin.Settings == null) return;
            if (!Plugin.Settings.ControllerEnabled.Value)
            {
                if (_enabled) TurnedOff();
                return;
            }
            if (!_enabled)
            {
                // Turned back on: a controller found before or while it was off may be gone, so look again.
                _enabled = true;
                _probeFound = -1;
            }
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
                List<PadAction> simActions = Mapping.Update(sim, Time.unscaledTime);
                if (simActions.Count > 0) Loc.PadMode = true;
                foreach (PadAction a in simActions) Dispatch(a);
                return;
            }
            if (_disabled) return;
            try
            {
                if (_padIndex < 0)
                {
                    // XInput is slow to answer for a slot with no controller, so looking for one runs on a
                    // worker thread: one slot every half second (all four every 2 seconds, as before).
                    int failure = _probeFailure;
                    if (failure != 0)
                    {
                        // Handled below, as if the call had failed here.
                        _probeFailure = 0;
                        if (failure == 1) throw new DllNotFoundException();
                        throw new EntryPointNotFoundException();
                    }
                    int found = _probeFound;
                    if (found < 0)
                    {
                        if (!_probeRunning && Time.unscaledTime >= _nextScan)
                        {
                            _nextScan = Time.unscaledTime + 0.5f;
                            _probeRunning = true;
                            uint slot = _scanSlot;
                            _scanSlot = (_scanSlot + 1) % 4;
                            System.Threading.ThreadPool.QueueUserWorkItem(Probe, slot);
                        }
                        return;
                    }
                    _probeFound = -1;
                    _padIndex = found;
                    // Its first state is taken as already held (see below).
                    _seedNext = true;
                    if (!_announcedConnect)
                    {
                        _announcedConnect = true;
                        ConnectionChanged(true);
                    }
                }

                if (!TryGet((uint)_padIndex, out XInputState s))
                {
                    _padIndex = -1;
                    _announcedConnect = false;
                    // Buttons held when it dropped out must not count as released when it comes back.
                    Mapping.Reset();
                    ConnectionChanged(false);
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
                if (_seedNext)
                {
                    // A button held while the controller (re)connects is not a new press.
                    _seedNext = false;
                    Mapping.Seed(state, Time.unscaledTime);
                    return;
                }
                List<PadAction> actions = Mapping.Update(state, Time.unscaledTime);
                if (actions.Count > 0) Loc.PadMode = true;
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

        private static volatile int _probeFound = -1;
        private static volatile bool _probeRunning;
        // 1: the XInput library is missing, 2: its entry point is (seen by the worker, handled in Update).
        private static volatile int _probeFailure;

        /// <summary>Worker thread: is there a controller in this slot?</summary>
        private static void Probe(object slot)
        {
            try
            {
                if (TryGet((uint)slot, out _)) _probeFound = (int)(uint)slot;
            }
            catch (DllNotFoundException)
            {
                _probeFailure = 1;
            }
            catch (EntryPointNotFoundException)
            {
                _probeFailure = 2;
            }
            catch (Exception)
            {
                // Nothing found this time.
            }
            finally
            {
                _probeRunning = false;
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
                case PadAction.Customers:
                    // On the map (no customers in the evening) the left stick click starts and ends walk mode.
                    if (AccessibilityRoot.Instance?.Contexts.Current?.Id == "map") ModInput.Inject(KeyCode.W, ctrl: true);
                    else ModInput.Inject(KeyCode.K, ctrl: true);
                    break;
                case PadAction.TogglePause: ModInput.Inject(KeyCode.P, ctrl: true); break;
                case PadAction.CategoryPrevious: ModInput.Inject(KeyCode.UpArrow, ctrl: true); break;
                case PadAction.CategoryNext: ModInput.Inject(KeyCode.DownArrow, ctrl: true); break;
                case PadAction.HistoryOlder: ModInput.Inject(KeyCode.Comma); break;
                case PadAction.HistoryNewer: ModInput.Inject(KeyCode.Period); break;
                case PadAction.KeyList: ModInput.Inject(KeyCode.F1, shift: true); break;
                case PadAction.ModSettings: ModInput.Inject(KeyCode.F5); break;
                case PadAction.RecallStory: ModInput.Inject(KeyCode.Backslash); break;
                case PadAction.Goal: ModInput.Inject(KeyCode.J, ctrl: true); break;
                case PadAction.ActivateThird: ModInput.Inject(KeyCode.Return, ctrl: true, noFallback: true); break;
                case PadAction.ReadScreen: ModInput.Inject(KeyCode.R, shift: true, ctrl: true); break;
                case PadAction.FirstItem: ModInput.Inject(KeyCode.Home); break;
                case PadAction.LastItem: ModInput.Inject(KeyCode.End); break;
                case PadAction.HistoryCategory: ModInput.Inject(KeyCode.RightBracket); break;
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
