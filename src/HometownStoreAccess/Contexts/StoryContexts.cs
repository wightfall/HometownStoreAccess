using System.Collections.Generic;
using DG.Tweening;
using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    /// <summary>
    /// Story dialog. Each line is read in full as soon as it starts (no waiting for the typewriter).
    /// Enter continues (one press finishes the typing and advances), responses are a list.
    /// </summary>
    internal sealed class DialogContext : ScreenContext
    {
        private DialogEvent _lastDialog;
        private int _lastLine = -1;
        private bool _pendingAdvance;
        private int _lastResponseCount;
        private string _announcedLine;

        public override string Id => "dialog";
        public override int Priority => 80;
        public override bool GuardsEnterOnExit => true;
        public override bool ReservesSpace => true;
        public override bool ResetFocusOnEnter => true;
        public override bool AnnounceFocusOnEnter => false;
        public override MsgCategory IntroCategory => MsgCategory.Dialog;

        private static DialogPanel Panel
        {
            get
            {
                GameProgressionControl gp = GameProgressionControl.Instance;
                if (gp != null && gp.dialogPanel != null) return gp.dialogPanel;
                return Object.FindObjectOfType<DialogPanel>();
            }
        }

        public override bool IsActive()
        {
            DialogPanel p = Panel;
            if (p == null || !p.gameObject.activeInHierarchy) return false;
            DialogueLogManager log = p.GetComponent<DialogueLogManager>();
            return log == null || !log.IsLogPanelActive();
        }

        public override void OnEnter()
        {
            _lastDialog = null;
            _lastLine = -1;
            _pendingAdvance = false;
            _lastResponseCount = 0;
            _announcedLine = null;
        }

        public override string Title() => null;

        public override string Intro()
        {
            DialogPanel p = Panel;
            if (p == null || p._shownDialog == null) return null;
            _lastDialog = p._shownDialog;
            _lastLine = p._shownLineIndex;
            _announcedLine = CurrentLineText();
            return _announcedLine;
        }

        private static string CurrentLineText()
        {
            DialogPanel p = Panel;
            if (p == null || p._shownDialog == null) return Loc.Get("dialog_waiting");
            string name = UiUtil.Text(p.CharacterLabel);
            string text = TextCleaner.Clean(p._isTyping ? p._typingDescription : p.descriptionLabel.text);
            return name.Length > 0 ? Loc.Get("dialog_line", name, text) : text;
        }

        public override void Tick()
        {
            DialogPanel p = Panel;
            if (p == null) return;
            if (p._shownDialog != _lastDialog || p._shownLineIndex != _lastLine)
            {
                _lastDialog = p._shownDialog;
                _lastLine = p._shownLineIndex;
                string line = p._shownDialog != null ? CurrentLineText() : null;
                if (!string.IsNullOrEmpty(line) && line != _announcedLine && Plugin.Settings.ReadDialogAutomatically.Value)
                {
                    _announcedLine = line;
                    SpeechService.Say(line, true, MsgCategory.Dialog);
                }
            }
            if (_pendingAdvance && !p._isTyping)
            {
                _pendingAdvance = false;
                p.OnNextPressed();
            }
            int responses = ResponseCount(p);
            if (responses != _lastResponseCount)
            {
                _lastResponseCount = responses;
                if (responses > 0)
                {
                    SpeechService.Say(Loc.Get("dialog_choose", responses), false);
                    AccessibilityRoot.Instance.Contexts.Refresh(announceFocus: false);
                    AccessibilityRoot.Instance.Contexts.MoveTo(0);
                }
            }
        }

        private static int ResponseCount(DialogPanel p)
        {
            int n = 0;
            foreach (Transform t in p.answerParent)
            {
                if (t.gameObject.activeInHierarchy && t.GetComponent<ContentRespond>() != null) n++;
            }
            return n;
        }

        public override void Build(List<NavItem> items)
        {
            DialogPanel p = Panel;
            if (p == null) return;
            string answers = Loc.Get("group_responses");
            bool any = false;
            foreach (Transform t in p.answerParent)
            {
                var r = t.GetComponent<ContentRespond>();
                if (r == null || !t.gameObject.activeInHierarchy) continue;
                any = true;
                ContentRespond resp = r;
                items.Add(new ButtonItem(resp.Button, () => UiUtil.Text(resp._title), "resp:" + resp.GetInstanceID())
                {
                    Group = answers,
                    ValueFunc = () => resp._currentResponse != null && resp._currentResponse.costMoney > 0 ? Loc.Get("response_cost", resp._currentResponse.costMoney) : null,
                    DetailFunc = () => resp._currentResponse != null ? TextCleaner.Clean(resp._currentResponse.answerText.GetDescription(string.Empty, LanguageControl.CurrentLanguage)) : null,
                });
            }
            if (!any)
            {
                items.Add(new ActionItem(() => Loc.Get("dialog_continue"), Advance, "continue") { DetailFunc = CurrentLineText });
            }
            string tools = Loc.Get("group_dialog_tools");
            items.Add(new TextItem(CurrentLineText, "line") { Group = tools });
            if (p.skipPressedButton != null && p.skipPressedButton.gameObject.activeInHierarchy)
            {
                items.Add(new ButtonItem(p.skipPressedButton, () => Loc.Get("label_dialog_skip")) { Group = tools });
            }
            UiScan.AddControl(items, p.logButtonObject, tools, () => Loc.Get("label_dialog_log"));
        }

        private void Advance()
        {
            DialogPanel p = Panel;
            if (p == null) return;
            if (p._isTyping)
            {
                // First press shows the whole line; the second (sent once typing stops) advances.
                p.OnNextPressed();
                _pendingAdvance = true;
            }
            else
            {
                p.OnNextPressed();
            }
        }

        public override bool HandleInput()
        {
            if ((ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter)) && ResponseCount(Panel) == 0)
            {
                Advance();
                return true;
            }
            return false;
        }

        public override string ReadAll() => CurrentLineText();

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_dialog");
        }
    }

    /// <summary>The dialog history log.</summary>
    internal sealed class DialogLogContext : ScreenContext
    {
        public override string Id => "dialog_log";
        public override int Priority => 82;

        private static DialogueLogManager LogManager => Object.FindObjectOfType<DialogueLogManager>();

        public override bool IsActive()
        {
            DialogueLogManager l = LogManager;
            return l != null && l.LogPanel != null && l.LogPanel.activeInHierarchy;
        }

        public override string Title() => Loc.Get("label_dialog_log");

        public override void Build(List<NavItem> items)
        {
            DialogueLogManager l = LogManager;
            if (l == null) return;
            foreach (Transform row in l.logContainer)
            {
                if (!row.gameObject.activeInHierarchy) continue;
                UiScan.AddText(items, row.GetComponent<TMP_Text>());
            }
            Transform close = l.LogPanel.transform.Find("Close Log Button");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>());
        }

        public override void OnEnter()
        {
            // Start at the most recent line.
            AccessibilityRoot.Instance.Contexts.Refresh(false);
        }
    }

    /// <summary>Reward popups (money, items, blueprints, new customers, new places, vehicles).</summary>
    internal sealed class CongratulationContext : ScreenContext
    {
        private float _readAt;
        private CongratulationPanel _read;

        public override string Id => "congratulation";
        public override MsgCategory IntroCategory => MsgCategory.Events;
        public override int Priority => 85;
        public override bool GuardsEnterOnExit => true;
        public override bool ReservesSpace => true;
        public override bool ResetFocusOnEnter => true;

        private static CongratulationPanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.currentCongratulationPanel : null;

        public override bool IsActive()
        {
            CongratulationPanel p = Panel;
            return p != null && p.gameObject.activeInHierarchy;
        }

        public override string Title() => Loc.Get("ctx_reward");

        public override void OnEnter()
        {
            _read = null;
            _readAt = Time.unscaledTime + 0.15f;
        }

        public override void Tick()
        {
            CongratulationPanel p = Panel;
            if (p == null) return;
            if (p != _read && Time.unscaledTime >= _readAt)
            {
                if (_read != null)
                {
                    // Next reward in the queue: wait a moment for its texts, then read.
                    _read = p;
                    _readAt = Time.unscaledTime + 0.15f;
                    return;
                }
                _read = p;
                SpeechService.Say(Content(p), false, MsgCategory.Events);
            }
        }

        private static string Content(CongratulationPanel p)
        {
            var texts = UiUtil.CollectTexts(p.gameObject, false, 30);
            texts.RemoveAll(t => t == "999" || t == "x999");
            return TextCleaner.Join(". ", texts.ToArray());
        }

        public override void Build(List<NavItem> items)
        {
            CongratulationPanel p = Panel;
            if (p == null) return;
            items.Add(new ActionItem(() => Loc.Get("reward_continue"), Continue, "continue") { DetailFunc = () => Content(p) });
            items.Add(new TextItem(() => Content(p), "content"));
        }

        public override bool HandleInput()
        {
            if (ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter))
            {
                Continue();
                return true;
            }
            return false;
        }

        /// <summary>Finishes the pop-in animation if needed and closes this reward (the next one, if any, follows).</summary>
        private static void Continue()
        {
            CongratulationPanel cur = Panel;
            if (cur == null) return;
            bool animating = cur.animationSequence != null && cur.animationSequence.IsPlaying();
            cur.AgreeToCongratulation();
            if (animating && cur != null && cur == Panel) cur.AgreeToCongratulation();
        }

        public override string ReadAll() => Panel != null ? Content(Panel) : null;
    }
}
