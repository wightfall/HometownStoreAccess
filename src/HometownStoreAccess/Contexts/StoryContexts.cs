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

        // The pictures shown with the last line, by character (a character can move to another place on the
        // screen), and the story picture: what changes is said.
        private readonly Dictionary<string, string> _shown = new Dictionary<string, string>(System.StringComparer.Ordinal);
        // The characters seen in this conversation (one who fades out leaves it): only a new one "appears".
        private readonly HashSet<string> _seenHere = new HashSet<string>(System.StringComparer.Ordinal);
        private string _storyPicture;
        private static readonly PortraitPosition[] Places = { PortraitPosition.Left, PortraitPosition.Center, PortraitPosition.Right };

        public override string Id => "dialog";
        public override int Priority => 80;
        public override bool GuardsEnterOnExit => true;
        public override bool ReservesSpace => true;
        public override bool ResetFocusOnEnter => true;
        public override bool AnnounceFocusOnEnter => false;
        // The first reply must be the first item (Enter picks it): no heading in front of it.
        public override bool HeadingItems => false;

        /// <summary>A reply that leads straight into the next lines: the focus goes to Continue, not the tools.</summary>
        public override string PreferredFocusKey() => ResponseCount(Panel) == 0 ? "continue" : null;
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
            _resumed = _resumeConversation;
            // Coming back to the same conversation (its log, a reward, the settings): its pictures are still
            // the ones already said. A new one starts afresh.
            if (!_resumed) _picturesDialog = null;
            if (!_resumeConversation) SpeechService.Recall.StartConversation();
            _resumeConversation = false;
        }

        // Set while coming back to the conversation left: the line then is said as it was the first time.
        private bool _resumed;
        private DialogEvent _resumeDialog;
        private int _resumeLine = -1;
        private string _resumeText;

        /// <summary>Left for the game's Log view or a popup while the conversation is still open.</summary>
        private bool _resumeConversation;

        public override void OnExit()
        {
            DialogPanel p = Panel;
            _resumeConversation = p != null && p.gameObject.activeInHierarchy;
            _resumeDialog = _lastDialog;
            _resumeLine = _lastLine;
            _resumeText = _announcedLine;
        }

        public override string Title() => null;

        public override string Intro()
        {
            DialogPanel p = Panel;
            if (p == null || p._shownDialog == null) return null;
            _lastDialog = p._shownDialog;
            _lastLine = p._shownLineIndex;
            if (_resumed && p._shownDialog == _resumeDialog && p._shownLineIndex == _resumeLine && !string.IsNullOrEmpty(_resumeText))
            {
                // Back on the line already heard: the same words (the history keeps it once).
                _announcedLine = _resumeText;
                SpeechService.Recall.AddLine(_announcedLine);
                return _announcedLine;
            }
            // Only with a line on screen: as a new conversation starts, the panel still holds the last one's
            // data (its text already cleared), and its pictures are not this conversation's.
            string text = CurrentLineText();
            _announcedLine = string.IsNullOrEmpty(TextCleaner.Clean(text)) ? text : TextCleaner.Join(". ", PictureNote(p), text);
            SpeechService.Recall.AddLine(_announcedLine);
            return _announcedLine;
        }

        // Who each portrait shows, in the game's language (forgotten when it changes); see PortraitNames. Learned
        // once from every conversation loaded, and from each new one.
        private static readonly PortraitNames Names = new PortraitNames();
        private static readonly HashSet<DialogEvent> Learned = new HashSet<DialogEvent>();
        private static readonly Dictionary<string, string> CustomerNames = new Dictionary<string, string>(System.StringComparer.Ordinal);
        private static string _namesLanguage;
        private static bool _learnedAll;
        // The shown conversation's own names (a character can go by another name in it).
        private readonly PortraitNames _local = new PortraitNames();
        private DialogEvent _localDialog;
        private string _localLanguage;
        // The conversation the picture state belongs to: another one (queued right after, say) starts afresh.
        private DialogEvent _picturesDialog;

        /// <summary>
        /// What the shown line's pictures show that the last line's did not: who appears while someone else
        /// speaks, a character's new face, a shaking picture, or a story picture (settings "Faces and story
        /// pictures in conversations" and "Shaking pictures in conversations"). Null when nothing is new.
        /// </summary>
        private string PictureNote(DialogPanel p)
        {
            if (p == null || p._shownDialog == null || p._shownDialog.dialogLines == null) return null;
            int index = p._shownLineIndex;
            if (index < 0 || index >= p._shownDialog.dialogLines.Count) return null;
            DialogLine line = p._shownDialog.dialogLines[index];
            if (line == null) return null;
            Learn(p._shownDialog);
            if (p._shownDialog != _localDialog || _namesLanguage != _localLanguage)
            {
                _local.Clear();
                _localDialog = p._shownDialog;
                _localLanguage = _namesLanguage;
                LearnLines(_local, _localDialog);
            }
            if (p._shownDialog != _picturesDialog)
            {
                _shown.Clear();
                _seenHere.Clear();
                _storyPicture = null;
                _picturesDialog = p._shownDialog;
            }
            string speaker = UiUtil.Text(p.CharacterLabel);
            bool faces = Plugin.Settings.AnnouncePictures.Value;
            bool shakes = Plugin.Settings.AnnounceShaking.Value;
            // The game dims the listeners: the one picture not dimmed is the speaker's, when the speaker is a
            // character (not the player or the narrator).
            string lit = LitCharacter(line, out PortraitPosition litPlace);
            bool speakerIsCharacter = speaker.Length > 0 && !Names.IsGeneric(speaker);
            var now = new Dictionary<string, string>(System.StringComparer.Ordinal);
            // Notes about others (with their names), and the speaker's own (no name): the speaker's come last,
            // right before the line, so an unnamed "smiling" is heard as the speaker's.
            var others = new List<string>();
            var own = new List<string>();
            var leaving = new List<string>();
            foreach (PortraitPosition place in Places)
            {
                PortraitData data = line.GetPortraitData(place);
                // A picture fading out counts as gone (see below, after the loop).
                if (data == null || data.portrait == null || data.transitionType == PortraitTransitionType.FadeOut) continue;
                string picture = data.portrait.name;
                string character = PortraitFaces.CharacterKey(picture);
                if (character.Length == 0 || now.ContainsKey(character)) continue;
                now[character] = picture;
                _shown.TryGetValue(character, out string before);
                // New in the conversation (or back after leaving), not only back after another picture.
                bool isNew = !_seenHere.Contains(character);
                List<string> keys = PortraitFaces.NoteKeys(before, picture, data.transitionType == PortraitTransitionType.Shake, faces, shakes);
                if (keys.Count == 0 && !(faces && isNew)) continue;
                bool speaking = speakerIsCharacter && character == lit && place == litPlace;
                // Several pictures lit: one heard under the speaker's name is the speaker's.
                if (!speaking && speakerIsCharacter && (_local.HasHeard(character, speaker) || Names.HasHeard(character, speaker))) speaking = true;
                string name = speaking ? null : (_local.NameOf(character, Names.IsGeneric) ?? NameForCharacter(character, picture));
                // Several pictures lit, or none: the name tells whether it is the speaker's.
                if (name != null && string.Equals(name, speaker, System.StringComparison.OrdinalIgnoreCase))
                {
                    speaking = true;
                    name = null;
                }
                // Someone who comes in while another speaks: the line does not say who that is.
                bool appeared = faces && isNew && !speaking && name != null;
                if (keys.Count == 0 && !appeared) continue;
                var texts = new List<string>();
                foreach (string k in keys) texts.Add(Loc.Get(k));
                string what = TextCleaner.Join(", ", texts.ToArray());
                if (appeared) others.Add(TextCleaner.Join(", ", Loc.Get("face_appears", name), what));
                else if (name != null) others.Add(Loc.Get("face_of", name, what));
                else own.Add(what);
            }
            // Whoever was shown and is not any more (faded out, or simply gone, also when lines were skipped):
            // they leave. One who only moved to another place is still there.
            foreach (KeyValuePair<string, string> kv in _shown)
            {
                if (now.ContainsKey(kv.Key)) continue;
                _seenHere.Remove(kv.Key);
                if (!faces) continue;
                string goneName = _local.NameOf(kv.Key, Names.IsGeneric) ?? NameForCharacter(kv.Key, kv.Value);
                if (goneName != null) leaving.Add(goneName);
            }
            var notes = new List<string>();
            if (leaving.Count > 0) notes.Add(Loc.Get(PortraitFaces.LeaveKey(leaving.Count), string.Join(", ", leaving.ToArray())));
            notes.AddRange(others);
            notes.AddRange(own);
            // Followed even while the settings are off, so turning them on says only what changes from then on.
            _shown.Clear();
            foreach (KeyValuePair<string, string> kv in now)
            {
                _shown[kv.Key] = kv.Value;
                _seenHere.Add(kv.Key);
            }
            string storyPicture = line.cg_img != null ? line.cg_img.name : null;
            string pictureKey = PortraitFaces.StoryPictureKey(_storyPicture, storyPicture);
            _storyPicture = storyPicture;
            if (pictureKey != null && faces) notes.Insert(0, Loc.Get(pictureKey));
            return notes.Count == 0 ? null : TextCleaner.Join(". ", notes.ToArray());
        }

        /// <summary>Learns who each portrait shows: once from every conversation loaded, then from each new one.</summary>
        private static void Learn(DialogEvent current)
        {
            string language = GameLanguage.Selected != null ? GameLanguage.Selected.Identifier.Code : null;
            if (language != _namesLanguage)
            {
                Names.Clear();
                Learned.Clear();
                CustomerNames.Clear();
                _learnedAll = false;
                _namesLanguage = language;
            }
            if (!_learnedAll)
            {
                _learnedAll = true;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    foreach (DialogEvent d in Resources.FindObjectsOfTypeAll<DialogEvent>()) LearnDialog(d);
                }
                catch (System.Exception e)
                {
                    Log.Debug("Learning portrait names: " + e.Message);
                }
                Log.Debug("Portrait names learned from " + Learned.Count + " conversations in " + sw.ElapsedMilliseconds + " ms");
            }
            if (current != null) LearnDialog(current);
        }

        private static void LearnDialog(DialogEvent d)
        {
            if (d == null || d.dialogLines == null || !Learned.Add(d)) return;
            LearnLines(Names, d);
        }

        private static void LearnLines(PortraitNames names, DialogEvent d)
        {
            if (d == null || d.dialogLines == null) return;
            foreach (DialogLine dl in d.dialogLines)
            {
                if (dl == null || dl.dialogLine == null) continue;
                string speaker;
                try
                {
                    speaker = UsableName(dl.dialogLine.GetName(d.name, LanguageControl.CurrentLanguage));
                }
                catch
                {
                    continue;
                }
                names.LearnLine(speaker, LitCharacter(dl, out _));
            }
        }

        /// <summary>The character of the only picture on the line that is not dimmed (null: none, or several), and its place.</summary>
        private static string LitCharacter(DialogLine line, out PortraitPosition place)
        {
            place = PortraitPosition.Center;
            string lit = null;
            int count = 0;
            foreach (PortraitPosition pp in Places)
            {
                PortraitData d = line.GetPortraitData(pp);
                if (d == null || d.portrait == null || d.transitionType == PortraitTransitionType.Dim || d.transitionType == PortraitTransitionType.FadeOut) continue;
                count++;
                lit = PortraitFaces.CharacterKey(d.portrait.name);
                place = pp;
            }
            return count == 1 ? lit : null;
        }

        /// <summary>
        /// The name a portrait's character goes by: as the conversations call them (never the player or the
        /// narrator), else the portrait collection's, else the customer name ("Collegian").
        /// </summary>
        internal static string NameForCharacter(string character, string picture)
        {
            Learn(null);
            return Names.NameOf(character) ?? CollectionName(character) ?? CustomerName(picture);
        }

        private static string CollectionName(string character)
        {
            try
            {
                CharacterCollectionManager cm = CharacterCollectionManager.Instance;
                if (cm == null || cm.allCharacterPortraits == null) return null;
                foreach (CharacterPortrait cp in cm.allCharacterPortraits)
                {
                    if (cp == null || cp.detailsKey == null) continue;
                    bool same = PortraitFaces.CharacterKey(cp.name) == character || (cp.portrait != null && PortraitFaces.CharacterKey(cp.portrait.name) == character);
                    if (!same) continue;
                    string n = UsableName(cp.detailsKey.GetName(cp.name, LanguageControl.CurrentLanguage));
                    if (n.Length > 0) return n;
                }
            }
            catch (System.Exception e)
            {
                Log.Debug("Portrait collection name: " + e.Message);
            }
            return null;
        }

        /// <summary>A name as the game gives it, cleaned; empty for none or the game's error text ("Key '...' not found").</summary>
        private static string UsableName(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.StartsWith("Key '", System.StringComparison.Ordinal) || raw.StartsWith("Table '", System.StringComparison.Ordinal)) return string.Empty;
            return TextCleaner.Clean(raw);
        }

        /// <summary>A portrait's character by the game's customer names ("Collegian", "Street Racer"), when it is one of them.</summary>
        private static string CustomerName(string picture)
        {
            PortraitFaces.Split(picture, out string character, out _);
            if (character.Length == 0) return null;
            if (CustomerNames.TryGetValue(character, out string cached)) return cached;
            string found = null;
            foreach (string key in new[] { character, PortraitFaces.SpacedWords(character) })
            {
                try
                {
                    string n = LocalizationManager.GetName(key);
                    if (string.IsNullOrEmpty(n) || n.StartsWith("Key '", System.StringComparison.Ordinal) || n.StartsWith("Table '", System.StringComparison.Ordinal)) continue;
                    n = TextCleaner.Clean(n);
                    if (n.Length == 0) continue;
                    found = n;
                    break;
                }
                catch (System.Exception e)
                {
                    Log.Debug("Customer name for " + key + ": " + e.Message);
                }
            }
            CustomerNames[character] = found;
            return found;
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
                if (p._shownDialog == null) _picturesDialog = null;
                string line = p._shownDialog != null ? CurrentLineText() : null;
                // What the line's pictures show anew (a face, a story picture) comes first.
                if (!string.IsNullOrEmpty(line)) line = TextCleaner.Join(". ", PictureNote(p), line);
                if (!string.IsNullOrEmpty(line)) SpeechService.Recall.AddLine(line);
                // Every new line, also one that reads like the last ("..."): the line number tells them apart.
                if (!string.IsNullOrEmpty(line) && Plugin.Settings.ReadDialogAutomatically.Value)
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
        private static DialogPanel _panelOfLog;
        private static DialogueLogManager _log;

        private static DialogueLogManager LogManager
        {
            get
            {
                GameProgressionControl gp = GameProgressionControl.Instance;
                DialogPanel panel = gp != null ? gp.dialogPanel : null;
                if (panel != null)
                {
                    // Asked every frame: look the component up once per dialog panel.
                    if (panel != _panelOfLog)
                    {
                        _panelOfLog = panel;
                        _log = panel.GetComponent<DialogueLogManager>();
                    }
                    if (_log != null) return _log;
                }
                return SceneCache.Get<DialogueLogManager>();
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

        // The button (Continue) is said after what the reward gave, once that has settled (see Tick), not before it.
        public override bool AnnounceFocusOnEnter => false;
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
            // Then the button, so what the reward gave comes first, as on screen.
            AccessibilityRoot.Instance.Contexts.AnnounceFocus(includeGroup: false, interrupt: false);
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
            // The item and building lines read "Candy x3": "Candy: 3" (the "x" is read as a letter).
            string itemLine = p.itemText != null && p.itemText.gameObject.activeInHierarchy ? UiUtil.Text(p.itemText) : null;
            string buildLine = p.buildText != null && p.buildText.gameObject.activeInHierarchy ? UiUtil.Text(p.buildText) : null;
            for (int i = 0; i < texts.Count; i++)
            {
                if (texts[i] == itemLine || texts[i] == buildLine) texts[i] = RewardText.Quantity(texts[i]);
            }
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

        /// <summary>
        /// Finishes the pop-in animation if needed and closes this reward (the next one, if any, follows).
        /// Enter before the reward was said (pressed quickly through a conversation) says it instead and
        /// leaves it open: the next Enter closes it, so no reward goes by unheard.
        /// </summary>
        private void Continue()
        {
            CongratulationPanel cur = Panel;
            if (cur == null) return;
            if (cur != _read)
            {
                _read = cur;
                SpeechService.Say(Content(cur), true, MsgCategory.Events);
                AccessibilityRoot.Instance.Contexts.AnnounceFocus(includeGroup: false, interrupt: false);
                return;
            }
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
