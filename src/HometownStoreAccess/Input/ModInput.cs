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
        private static bool _injShift, _injCtrl, _injAlt, _injNoFallback;

        /// <summary>A key waiting to be injected, with its own modifiers.</summary>
        private struct PendingKey
        {
            public KeyCode Key;
            public bool Shift, Ctrl, Alt, NoFallback;

            public bool SameModifiers(PendingKey o) => Shift == o.Shift && Ctrl == o.Ctrl && Alt == o.Alt && NoFallback == o.NoFallback;
        }

        // In the order injected. A frame takes the keys at the front that have the first one's modifiers; a
        // key with other modifiers (or one already taken) waits for the next frame. So two controller actions
        // in one frame stay two presses with their own modifiers: a held X's Ctrl+Enter and A's Enter are not
        // one Ctrl+Enter, and X's Shift+Enter does not turn a D-pad Right into Shift+Right.
        private static readonly List<PendingKey> _pending = new List<PendingKey>();

        /// <summary>True while an input field has keyboard focus: mod hotkeys must stay quiet.</summary>
        public static bool TextEntryActive { get; set; }

        /// <param name="noFallback">An item without that alternative action does nothing (a controller hold:
        /// held a little too long, X must not do what A does).</param>
        public static void Inject(KeyCode key, bool shift = false, bool ctrl = false, bool alt = false, bool noFallback = false)
        {
            _pending.Add(new PendingKey { Key = key, Shift = shift, Ctrl = ctrl, Alt = alt, NoFallback = noFallback });
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
            _injShift = _injCtrl = _injAlt = _injNoFallback = false;
            if (_pending.Count == 0) return;
            PendingKey first = _pending[0];
            int taken = 0;
            while (taken < _pending.Count && _pending[taken].SameModifiers(first) && _injectedDown.Add(_pending[taken].Key)) taken++;
            _pending.RemoveRange(0, taken);
            _injShift = first.Shift;
            _injCtrl = first.Ctrl;
            _injAlt = first.Alt;
            // A real Enter in the same frame (the keyboard) keeps its fallback.
            _injNoFallback = first.NoFallback && !UnityEngine.Input.GetKeyDown(KeyCode.Return) && !UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        public static bool Down(KeyCode key)
        {
            return UnityEngine.Input.GetKeyDown(key) || _injectedDown.Contains(key);
        }

        public static bool Held(KeyCode key)
        {
            return UnityEngine.Input.GetKey(key);
        }

        /// <summary>The injected key of this frame must not fall back to an item's main action.</summary>
        public static bool NoFallback => _injNoFallback;

        public static bool Shift => _injShift || UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        public static bool Ctrl => _injCtrl || UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        public static bool Alt => _injAlt || UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);

        public static bool AnyInjected => _injectedDown.Count > 0;
    }
}
