using HometownStoreAccess.Audio;
using HometownStoreAccess.Contexts;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using UnityEngine;

namespace HometownStoreAccess.Input
{
    /// <summary>
    /// Keys that work on every screen: F1 help, Shift+F1 key list, F5 settings, message history
    /// (comma, period, brackets), repeat and read screen. In list screens plain letters jump to items
    /// by first letter, so letter commands there need Ctrl (Ctrl+R, Ctrl+T, ...).
    /// </summary>
    internal static class GlobalKeys
    {
        /// <summary>True when a plain letter should be a command (not first-letter navigation).</summary>
        public static bool LetterCommandsAllowed(AccessibilityRoot root)
        {
            if (ModInput.Ctrl) return true;
            ScreenContext c = root.Contexts.Current;
            return c != null && !c.UsesListNavigation;
        }

        /// <summary>Returns true when the key was consumed and the context should not see it.</summary>
        public static bool Handle(AccessibilityRoot root)
        {
            if (ModInput.Down(KeyCode.F1))
            {
                if (ModInput.Shift)
                {
                    if (Overlay.Open == Overlay.Kind.KeyList) Overlay.Close();
                    else Overlay.Show(Overlay.Kind.KeyList, root.Contexts.Current, root.InGame);
                }
                else
                {
                    Overlay.SpeakContextHelp(Overlay.IsOpen ? root.Contexts.Current : root.Contexts.Current, root.InGame);
                }
                return true;
            }
            if (ModInput.Down(KeyCode.F5))
            {
                if (Overlay.Open == Overlay.Kind.Settings) Overlay.Close();
                else Overlay.Show(Overlay.Kind.Settings, root.Contexts.Current, root.InGame);
                return true;
            }
            // The message history belongs to the game: its keys do nothing in the menus.
            if (root.GameplayActive && HistoryKeys()) return true;
            // Backslash: only inside a conversation.
            if (ModInput.Down(KeyCode.Backslash) && root.Contexts.Current != null && root.Contexts.Current.Id == "dialog")
            {
                RecallStory(ModInput.Shift);
                return true;
            }

            bool letters = LetterCommandsAllowed(root);
            if (letters && ModInput.Down(KeyCode.R) && !ModInput.Alt)
            {
                if (ModInput.Shift) ReadScreen(root);
                else
                {
                    string last = SpeechService.Buffer.LastSpoken;
                    SpeechService.SpeakReview(string.IsNullOrEmpty(last) ? Loc.Get("nothing_to_repeat") : last);
                }
                return true;
            }
            if (Overlay.IsOpen) return false;
            if (root.InGame && letters && GameKeys.Handle(root)) return true;
            return false;
        }

        /// <summary>, and . step through messages; [ and ] change category; Shift jumps to the first or last.</summary>
        private static bool HistoryKeys()
        {
            MessageLog log = SpeechService.History;
            bool shift = ModInput.Shift;
            if (ModInput.Down(KeyCode.LeftBracket) || ModInput.Down(KeyCode.RightBracket))
            {
                bool next = ModInput.Down(KeyCode.RightBracket);
                MsgCategory c = shift ? log.JumpCategory(next) : log.CycleCategory(next ? 1 : -1);
                string last = log.Jump(toLast: true);
                SpeechService.SpeakReview(TextCleaner.Join(". ", CategoryName(c), Loc.Get("history_count", log.Count(c)), last));
                return true;
            }
            if (ModInput.Down(KeyCode.Comma) || ModInput.Down(KeyCode.Period))
            {
                bool newer = ModInput.Down(KeyCode.Period);
                string text;
                bool edge = false;
                if (shift) text = log.Jump(toLast: newer);
                else text = log.Step(newer ? 1 : -1, out edge);
                if (text == null)
                {
                    SpeechService.SpeakReview(Loc.Get("history_empty", CategoryName(log.Current)));
                    return true;
                }
                if (edge) AudioCues.Play(Cue.Edge);
                SpeechService.SpeakReview(Loc.Get("history_entry", log.Position + 1, log.Count(log.Current), text));
                return true;
            }
            return false;
        }

        /// <summary>
        /// In a conversation: Backslash says the current line again, Shift+Backslash everything said so
        /// far from the first line.
        /// </summary>
        private static void RecallStory(bool whole)
        {
            StoryRecall recall = SpeechService.Recall;
            string text;
            if (whole)
            {
                var lines = new System.Collections.Generic.List<string>(recall.Conversation);
                text = lines.Count == 0 ? null : TextCleaner.Join(". ", lines.ToArray());
            }
            else
            {
                text = recall.Last;
            }
            SpeechService.SpeakReview(string.IsNullOrEmpty(text) ? Loc.Get("recall_nothing") : text);
        }

        public static string CategoryName(MsgCategory c) => Loc.Get("hist_" + c.ToString().ToLowerInvariant());

        private static void ReadScreen(AccessibilityRoot root)
        {
            ScreenContext c = root.Contexts.Current;
            string text = null;
            if (c != null)
            {
                text = c.ReadAll();
                if (string.IsNullOrEmpty(text))
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (NavItem item in root.Contexts.Items) parts.Add(item.Speak());
                    text = TextCleaner.Join(". ", parts.ToArray());
                }
            }
            SpeechService.Say(string.IsNullOrEmpty(text) ? Loc.Get("nothing_here") : text, true);
        }
    }
}
