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

        public PanelContext(string id, int priority, Func<GameObject> root, Func<string> title, bool includeTexts = false, Func<GameObject, bool> skip = null)
        {
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
