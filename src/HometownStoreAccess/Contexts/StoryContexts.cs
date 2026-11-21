using System.Collections.Generic;
using DG.Tweening;
using HometownStoreAccess.Core;
using HometownStoreAccess.Game;
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
                return SceneCache.Get<DialogPanel>();
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
            if (!_resumeConversation) SpeechService.Recall.StartConversation();
            _resumeConversation = false;
        }

        /// <summary>Left for the game's Log view or a popup while the conversation is still open.</summary>
        private bool _resumeConversation;

        public override void OnExit()
        {
            DialogPanel p = Panel;
            _resumeConversation = p != null && p.gameObject.activeInHierarchy;
        }

        public override string Title() => null;

        public override string Intro()
        {
            DialogPanel p = Panel;
            if (p == null || p._shownDialog == null) return null;
            _lastDialog = p._shownDialog;
            _lastLine = p._shownLineIndex;
            _announcedLine = CurrentLineText();
            SpeechService.Recall.AddLine(_announcedLine);
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
                if (!string.IsNullOrEmpty(line)) SpeechService.Recall.AddLine(line);
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
            // Reply buttons of the previous conversation can linger while a new one starts: replies
            // only count from the last line on.
            // (While a new one is set up, no dialog is shown and the old buttons are not yet destroyed.)
            if (p._shownDialog == null || p._shownLineIndex < p._shownDialog.dialogLines.Count - 1) responses = 0;
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
                if (r == null || !t.gameObject.activeInHierarchy || p._shownDialog == null) continue;
                any = true;
                ContentRespond resp = r;
                items.Add(new ButtonItem(resp.Button, () => UiUtil.Text(resp._title), "resp:" + resp.GetInstanceID())
                {
                    Group = answers,
                    // The game shows the outcome of a reply ("Received 100 baht") on hover: say it with the reply.
                    ValueFunc = () => TextCleaner.Join(", ",
                        resp._currentResponse != null && resp._currentResponse.costMoney > 0 ? Loc.Get("response_cost", resp._currentResponse.costMoney) : null,
                        ResponseOutcome(resp)),
                    DetailFunc = () => ResponseOutcome(resp),
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

        /// <summary>The reply's description, unless it is empty, a placeholder or the reply text itself.</summary>
        private static string ResponseOutcome(ContentRespond resp)
        {
            if (resp._currentResponse == null) return null;
            string d = TextCleaner.Clean(resp._currentResponse.answerText.GetDescription(string.Empty, LanguageControl.CurrentLanguage));
            if (string.IsNullOrEmpty(d) || d.Length < 2 || d == UiUtil.Text(resp._title)) return null;
            return d;
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
            if (!(ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter)) || ResponseCount(Panel) != 0) return false;
            // Enter on the Log or Skip button presses that button; anywhere else it moves the conversation on.
            NavItem f = AccessibilityRoot.Instance.Contexts.Focused;
            if (f != null && f.Group == Loc.Get("group_dialog_tools") && f.Key != "line") return false;
            Advance();
            return true;
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

        /// <summary>The log belongs to the dialog panel (no scene search every frame).</summary>
        private static DialogueLogManager LogManager
        {
            get
            {
                GameProgressionControl gp = GameProgressionControl.Instance;
                DialogueLogManager l = gp != null && gp.dialogPanel != null ? gp.dialogPanel.GetComponent<DialogueLogManager>() : null;
                return l != null ? l : SceneCache.Get<DialogueLogManager>();
            }
        }

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

        /// <summary>Start at the most recent line (the game adds new lines at the end); Up reads earlier ones.</summary>
        public override int InitialFocus(IReadOnlyList<NavItem> items)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] is TextItem) return i;
            }
            return -1;
        }

        public override bool ResetFocusOnEnter => true;

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_dialog_log");
        }
    }

    /// <summary>Reward popups (money, items, blueprints, new customers, new places, vehicles).</summary>
    internal sealed class CongratulationContext : ScreenContext
    {
        private CongratulationPanel _read;
        private CongratulationPanel _pending;
        private float _pendingSince;
        private float _stableSince;
        private string _lastContent;

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
            _pending = null;
        }

        public override void Tick()
        {
            CongratulationPanel p = Panel;
            if (p == null || p == _read) return;
            float now = Time.unscaledTime;
            if (p != _pending)
            {
                // A new reward (or the next one in the queue): give its texts a moment to appear.
                _pending = p;
                _pendingSince = now;
                _lastContent = null;
                return;
            }
            if (now - _pendingSince < 0.15f) return;
            string content = Content(p);
            if (content != _lastContent)
            {
                _lastContent = content;
                _stableSince = now;
            }
            // Counters (trust points) count up: read once the text settles, at the latest after 3 seconds.
            if (now - _stableSince < 0.4f && now - _pendingSince < 3f) return;
            _read = p;
            SpeechService.Say(content, false, MsgCategory.Events);
        }

        internal static string Content(CongratulationPanel p)
        {
            var skip = new HashSet<Transform>();
            var extra = new List<string>();
            if (p.budgetTemplate != null && p.budgetTemplate.activeInHierarchy)
            {
                extra.Add(RewardText.Budget(p.hasMin, UiUtil.Text(p.budgetMinBase), UiUtil.Text(p.budgetMinFinal), p.hasMax, UiUtil.Text(p.budgetMaxBase), UiUtil.Text(p.budgetMaxFinal)));
                if (p.budgetMin != null) skip.Add(p.budgetMin.transform);
                if (p.budgetMax != null) skip.Add(p.budgetMax.transform);
            }
            if (p.itemNeedTemplate != null && p.itemNeedTemplate.gameObject.activeInHierarchy && p.listItemNeedPopup != null)
            {
                // New wishes are only pictures: name the items.
                var names = new List<string>();
                foreach (GameObject g in p.listItemNeedPopup)
                {
                    if (g == null) continue;
                    skip.Add(g.transform);
                    ContentItem c = g.GetComponent<ContentItem>();
                    if (c != null && c._item != null) names.Add(GameText.Name(c._item));
                }
                if (names.Count > 0) extra.Add(string.Join(", ", names.ToArray()));
            }
            if (p.sliderTemplate != null && p.sliderTemplate.activeInHierarchy && p.profileToAnimate != null)
            {
                // Trust points count up for a second and a half: read the result straight away.
                if (p.sliderValueText != null) skip.Add(p.sliderValueText.transform);
                if (p.finalValueText != null) skip.Add(p.finalValueText.transform);
                extra.Add("+" + p.expToAdd);
                CustomerProfile now = CustomerManager.Instance != null ? CustomerManager.Instance.GetCustomerProfile(p.profileToAnimate.customerID) : null;
                if (now != null) extra.Add(Loc.Get("reward_level", now.trustLevel));
            }
            List<string> texts = RewardText.Prepare(UiUtil.CollectTexts(p.gameObject, false, 40, skip));
            texts.AddRange(extra);
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

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_reward");
        }
    }
}
