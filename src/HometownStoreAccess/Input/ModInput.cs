using System.Collections.Generic;
using UnityEngine;

namespace HometownStoreAccess.Input
{
    /// <summary>
    /// Keyboard state as seen by the mod: the real keyboard plus keys injected by the developer
    /// test hook. Controller input is translated to the same logical keys by <see cref="Controller"/>.
    /// </summary>
    internal static class ModInput
    {
        private static readonly HashSet<KeyCode> _injectedDown = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> _pendingInjected = new HashSet<KeyCode>();
        private static bool _injShift, _injCtrl, _injAlt;
        private static bool _pendShift, _pendCtrl, _pendAlt;

        /// <summary>True while an input field has keyboard focus: mod hotkeys must stay quiet.</summary>
        public static bool TextEntryActive { get; set; }

        public static void Inject(KeyCode key, bool shift = false, bool ctrl = false, bool alt = false)
        {
            _pendingInjected.Add(key);
            _pendShift |= shift;
            _pendCtrl |= ctrl;
            _pendAlt |= alt;
        }

        // Keyboard key codes (below the mouse and joystick codes), for telling keyboard use from controller use.
        private static KeyCode[] _keyboardKeys;

        // Pressing these does not mean the player took up the keyboard: Ctrl stops screen reader speech,
        // Insert and Caps Lock are screen reader keys, the number pad is the review cursor, and modifiers,
        // lock keys, Windows, Menu and Print Screen are used outside the game. (Number pad Enter still counts.)
        private static bool IgnoredForDevice(KeyCode k) =>
            (k >= KeyCode.Keypad0 && k <= KeyCode.KeypadPlus) || k == KeyCode.KeypadEquals ||
            k == KeyCode.Insert || (k >= KeyCode.Numlock && k <= KeyCode.Menu);

        /// <summary>True when a real keyboard key went down this frame (not a mouse or controller button).</summary>
        private static bool KeyboardKeyDown()
        {
            if (!UnityEngine.Input.anyKeyDown) return false;
            if (_keyboardKeys == null)
            {
                var list = new List<KeyCode>();
                foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (k != KeyCode.None && k < KeyCode.Mouse0 && !IgnoredForDevice(k)) list.Add(k);
                }
                _keyboardKeys = list.ToArray();
            }
            foreach (KeyCode k in _keyboardKeys)
            {
                if (UnityEngine.Input.GetKeyDown(k)) return true;
            }
            return false;
        }

        /// <summary>Called at the start of the mod's frame: promotes injected keys to "down this frame".</summary>
        public static void BeginFrame()
        {
            // Back to keyboard instructions as soon as the keyboard is used.
            if (Core.Loc.PadMode && KeyboardKeyDown()) Core.Loc.PadMode = false;
            _injectedDown.Clear();
            foreach (KeyCode k in _pendingInjected) _injectedDown.Add(k);
            _pendingInjected.Clear();
            _injShift = _pendShift; _injCtrl = _pendCtrl; _injAlt = _pendAlt;
            _pendShift = _pendCtrl = _pendAlt = false;
        }

        public static bool Down(KeyCode key)
        {
            return UnityEngine.Input.GetKeyDown(key) || _injectedDown.Contains(key);
        }

        public static bool Held(KeyCode key)
        {
            return UnityEngine.Input.GetKey(key);
        }

        public static bool Shift => _injShift || UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        public static bool Ctrl => _injCtrl || UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        public static bool Alt => _injAlt || UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);

        public static bool AnyInjected => _injectedDown.Count > 0;
    }
}
