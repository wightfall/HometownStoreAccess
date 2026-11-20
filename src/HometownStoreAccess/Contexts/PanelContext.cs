using System;
using System.Collections.Generic;
using HometownStoreAccess.Core;
using HometownStoreAccess.UI;
using UnityEngine;

namespace HometownStoreAccess.Contexts
{
    /// <summary>
    /// A panel whose controls (and optionally text lines) are discovered generically. Good enough for
    /// simple screens; complex screens get dedicated contexts.
    /// </summary>
    internal class PanelContext : ScreenContext
    {
        private readonly string _id;
        private readonly int _priority;
        private readonly Func<GameObject> _root;
        private readonly Func<string> _title;
        private readonly bool _includeTexts;
        private readonly Func<GameObject, bool> _skip;
        private readonly string _helpKey;

        public PanelContext(string id, int priority, Func<GameObject> root, Func<string> title, bool includeTexts = false, Func<GameObject, bool> skip = null, string helpKey = "help_panel")
        {
            _helpKey = helpKey;
            _id = id;
            _priority = priority;
            _root = root;
            _title = title;
            _includeTexts = includeTexts;
            _skip = skip;
        }

        public override string Id => _id;
        public override int Priority => _priority;
        public override GameObject Root => SafeRoot();

        public override bool IsActive()
        {
            GameObject r = SafeRoot();
            return r != null && UiUtil.IsShown(r);
        }

        public override string Title() => _title?.Invoke();

        public override void Build(List<NavItem> items)
        {
            UiScan.Add(items, SafeRoot(), string.Empty, _includeTexts, _skip);
        }

        public override string ReadAll() => UiUtil.JoinTexts(SafeRoot(), false, ". ");

        private string _spokenText;
        private string _pendingText;
        private float _pendingSince;
        private float _nextCheck;

        public override void OnEnter()
        {
            _spokenText = _includeTexts ? ReadAll() : null;
            _pendingText = null;
        }

        /// <summary>
        /// Text that appears while the panel is open (a letter being opened, the next tutorial page) is
        /// read once it has stopped changing, so a typing animation is not read half-finished.
        /// </summary>
        public override void Tick()
        {
            if (!_includeTexts || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.2f;
            string now = ReadAll();
            if (now == _spokenText)
            {
                _pendingText = null;
                return;
            }
            if (now != _pendingText)
            {
                _pendingText = now;
                _pendingSince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - _pendingSince < 0.5f) return;
            string before = _spokenText ?? string.Empty;
            _spokenText = now;
            _pendingText = null;
            if (string.IsNullOrEmpty(now)) return;
            // Only the new part when the earlier text is still there.
            string added = before.Length > 0 && now.StartsWith(before, StringComparison.Ordinal) ? now.Substring(before.Length).TrimStart('.', ' ') : now;
            if (added.Length == 0) return;
            AccessibilityRoot.Instance.Contexts.Refresh(false);
            Speech.SpeechService.Say(added, false);
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get(_helpKey);
        }

        private GameObject SafeRoot()
        {
            try { return _root(); } catch { return null; }
        }
    }

    /// <summary>A yes/no confirmation or an OK notification: the message is read on open and on change.</summary>
    internal sealed class PopupContext : ScreenContext
    {
        private readonly string _id;
        private readonly int _priority;
        private readonly Func<GameObject> _root;
        private readonly Func<TMPro.TMP_Text> _message;
        private readonly Func<UnityEngine.UI.Button[]> _buttons;
        private readonly string _titleKey;
        private string _lastMessage;

        public PopupContext(string id, int priority, string titleKey, Func<GameObject> root, Func<TMPro.TMP_Text> message, Func<UnityEngine.UI.Button[]> buttons)
        {
            _id = id;
            _priority = priority;
            _titleKey = titleKey;
            _root = root;
            _message = message;
            _buttons = buttons;
        }

        public override string Id => _id;
        public override int Priority => _priority;
        public override bool ResetFocusOnEnter => true;
        public override bool GuardsEnterOnExit => true;
        public override MsgCategory IntroCategory => MsgCategory.Notices;

        public override bool IsActive()
        {
            GameObject r = Safe(_root);
            return r != null && r.activeInHierarchy;
        }

        public override string Title() => Loc.Get(_titleKey);

        public override string Intro()
        {
            _lastMessage = MessageText();
            return _lastMessage;
        }

        public override void Tick()
        {
            string m = MessageText();
            if (m != _lastMessage)
            {
                _lastMessage = m;
                if (!string.IsNullOrEmpty(m)) Speech.SpeechService.Say(m, true, MsgCategory.Notices);
            }
        }

        public override void Build(List<NavItem> items)
        {
            UnityEngine.UI.Button[] buttons = Safe(_buttons);
            if (buttons != null)
            {
                foreach (var b in buttons)
                {
                    if (b != null && b.gameObject.activeInHierarchy) items.Add(new ButtonItem(b));
                }
            }
            items.Add(new TextItem(MessageText, "popup_message"));
        }

        public override string ReadAll() => MessageText();

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_popup");
        }

        private string MessageText()
        {
            var t = Safe(_message);
            return t != null ? TextCleaner.Clean(t.text) : string.Empty;
        }

        private static T Safe<T>(Func<T> f) where T : class
        {
            try { return f(); } catch { return null; }
        }
    }
}
