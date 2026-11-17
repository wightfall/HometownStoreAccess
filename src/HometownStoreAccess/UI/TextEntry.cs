using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using TMPro;
using UnityEngine.EventSystems;

namespace HometownStoreAccess.UI
{
    /// <summary>
    /// Tracks the focused TextMeshPro input field: announces when editing starts and ends and echoes
    /// typed or deleted characters, since the screen reader cannot see the game's text fields.
    /// While a field is focused, the mod's hotkeys are disabled so typing works normally.
    /// </summary>
    internal static class TextEntry
    {
        private static TMP_InputField _field;
        private static string _lastText = string.Empty;
        private static float _finishedAt = -10f;

        public static bool Active => _field != null && _field.isFocused;

        /// <summary>The field being edited, if any.</summary>
        public static TMP_InputField Field => Active ? _field : null;

        /// <summary>True right after editing ended, so the Enter that ended it is not handled twice.</summary>
        public static bool FinishedRecently => UnityEngine.Time.unscaledTime - _finishedAt < 0.25f;

        public static void Begin(TMP_InputField field)
        {
            if (field == null) return;
            EventSystem es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            field.caretPosition = field.text != null ? field.text.Length : 0;
        }

        public static void Update()
        {
            TMP_InputField focused = FindFocused();
            ModInput.TextEntryActive = focused != null;
            if (focused != _field)
            {
                if (_field != null && focused == null)
                {
                    _finishedAt = UnityEngine.Time.unscaledTime;
                    string done = TextCleaner.Clean(_field.text);
                    SpeechService.Say(Loc.Get("edit_done", done.Length == 0 ? Loc.Get("state_empty") : done));
                }
                _field = focused;
                if (_field != null)
                {
                    _lastText = _field.text ?? string.Empty;
                    string current = TextCleaner.Clean(_lastText);
                    SpeechService.Say(Loc.Get("edit_begin", current.Length == 0 ? Loc.Get("state_empty") : current));
                }
                return;
            }
            if (_field == null) return;
            string text = _field.text ?? string.Empty;
            if (text == _lastText) return;
            SpeechService.Say(EditEcho.DescribeChange(_lastText, text));
            _lastText = text;
        }

        private static TMP_InputField FindFocused()
        {
            EventSystem es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return null;
            var field = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
            return field != null && field.isFocused ? field : null;
        }
    }
}
