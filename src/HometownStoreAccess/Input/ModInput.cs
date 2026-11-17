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

        /// <summary>Called at the start of the mod's frame: promotes injected keys to "down this frame".</summary>
        public static void BeginFrame()
        {
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
