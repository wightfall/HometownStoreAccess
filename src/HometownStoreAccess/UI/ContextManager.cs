using System;
using System.Collections.Generic;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess.UI
{
    /// <summary>
    /// Picks the active screen context each frame, keeps its navigation list fresh, announces
    /// screen changes and handles the standard list keys (arrows, Home/End, Tab, Enter, I).
    /// </summary>
    internal sealed class ContextManager
    {
        private const float RebuildInterval = 0.25f;

        private readonly List<ScreenContext> _contexts = new List<ScreenContext>();
        private readonly List<NavItem> _items = new List<NavItem>();
        private readonly List<string> _keys = new List<string>();
        private readonly NavCursor _cursor = new NavCursor();
        private readonly Dictionary<string, string> _rememberedFocus = new Dictionary<string, string>();
        private float _nextRebuild;
        private bool _announceFocusAfterRebuild;
        private string _lastAnnouncedGroup;
        private float _enterGuardUntil;

        public ScreenContext Current { get; private set; }
        public IReadOnlyList<NavItem> Items => _items;
        public NavItem Focused => _cursor.Index >= 0 && _cursor.Index < _items.Count ? _items[_cursor.Index] : null;

        public void Register(ScreenContext context) => _contexts.Add(context);

        public void Clear()
        {
            if (Current != null) SafeExit(Current);
            Current = null;
            _contexts.Clear();
            _items.Clear();
            _keys.Clear();
            _cursor.Reset(_keys);
            _rememberedFocus.Clear();
        }

        public void Update()
        {
            ScreenContext active = SelectActive();
            if (active != Current)
            {
                SwitchTo(active);
            }
            if (Current == null) return;

            if (Time.unscaledTime >= _nextRebuild)
            {
                Rebuild(false);
            }
            try { Current.Tick(); } catch (Exception e) { Log.Error("Context " + Current.Id + " tick failed: " + e); }
            // The game closes sub-lists itself when Escape is released: say where the focus went.
            if (UnityEngine.Input.GetKeyUp(KeyCode.Escape)) _announceFocusAfterRebuild = true;

            if (TextEntry.Active) return;
            bool enter = ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter);
            if (enter && (Time.unscaledTime < _enterGuardUntil || TextEntry.FinishedRecently)) return;
            bool consumed = false;
            try { consumed = Current.HandleInput(); } catch (Exception e) { Log.Error("Context " + Current.Id + " input failed: " + e); }
            if (!consumed && Current.UsesListNavigation) HandleListInput();
        }

        /// <summary>Forces an immediate rebuild and announces the focused item if it changed.</summary>
        public void Refresh(bool announceFocus)
        {
            if (Current == null) return;
            _announceFocusAfterRebuild |= announceFocus;
            Rebuild(false);
        }

        /// <summary>Asks the next rebuild to re-announce focus when the focused item vanished (after an action).</summary>
        public void AnnounceFocusIfChanged() => _announceFocusAfterRebuild = true;

        public string DebugState()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("registered=").Append(_contexts.Count).Append(" [");
            foreach (ScreenContext c in _contexts)
            {
                string a;
                try { a = c.IsActive() ? "ON" : "off"; } catch (Exception e) { a = "ERR " + e.GetType().Name + ": " + e.Message; }
                sb.Append(c.Id).Append('=').Append(a).Append("; ");
            }
            sb.Append("] ");
            if (Current == null) return sb.Append("no context").ToString();
            var f = Focused;
            return sb.Append(Current.Id + " items=" + _items.Count + " index=" + _cursor.Index + " focus=" + (f != null ? f.Speak() : "-")).ToString();
        }

        private ScreenContext SelectActive()
        {
            ScreenContext best = null;
            foreach (ScreenContext c in _contexts)
            {
                bool active;
                try { active = c.IsActive(); }
                catch (Exception e)
                {
                    Log.Debug("IsActive " + c.Id + " threw " + e.Message);
                    active = false;
                }
                if (active && (best == null || c.Priority > best.Priority)) best = c;
            }
            return best;
        }

        private void SwitchTo(ScreenContext next)
        {
            if (Current != null)
            {
                if (_cursor.FocusedKey != null) _rememberedFocus[Current.Id] = _cursor.FocusedKey;
                if (Current.GuardsEnterOnExit) _enterGuardUntil = Time.unscaledTime + 0.4f;
                SafeExit(Current);
            }
            Current = next;
            _items.Clear();
            _keys.Clear();
            _cursor.Reset(_keys);
            _lastAnnouncedGroup = null;
            if (Current == null) return;
            Log.Debug("Context -> " + Current.Id);
            try { Current.OnEnter(); } catch (Exception e) { Log.Error("Context " + Current.Id + " enter failed: " + e); }

            BuildItems();
            string remembered = null;
            if (!Current.ResetFocusOnEnter) _rememberedFocus.TryGetValue(Current.Id, out remembered);
            int index = remembered != null ? _keys.IndexOf(remembered) : -1;
            _cursor.Reset(_keys, preferFirst: true);
            if (index > 0) _cursor.MoveTo(index, _keys);

            string title = SafeString(Current.Title);
            string intro = SafeString(Current.Intro);
            SpeechService.Say(TextCleaner.Join(". ", title, intro), true, Current.IntroCategory);
            if (Current.UsesListNavigation && Current.AnnounceFocusOnEnter) AnnounceFocus(includeGroup: true, interrupt: false);
            else _lastAnnouncedGroup = Focused?.Group;
            ApplyVisualFocus();
            _nextRebuild = Time.unscaledTime + RebuildInterval;
        }

        private void Rebuild(bool preferFirst)
        {
            _nextRebuild = Time.unscaledTime + RebuildInterval;
            BuildItems();
            string before = _cursor.FocusedKey;
            bool changed = _cursor.Reset(_keys, preferFirst);
            if (changed && before != null && !_keys.Contains(before))
            {
                string preferred = null;
                try { preferred = Current.PreferredFocusKey(); } catch (Exception e) { Log.Debug("PreferredFocusKey " + Current.Id + " threw " + e.Message); }
                int at = preferred != null ? _keys.IndexOf(preferred) : -1;
                if (at >= 0) _cursor.MoveTo(at, _keys);
            }
            // Keep list focus on the text field being typed in (the game may activate one itself).
            TMPro.TMP_InputField editing = TextEntry.Field;
            if (editing != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i] is InputItem input && input.Field == editing && i != _cursor.Index)
                    {
                        _cursor.MoveTo(i, _keys);
                        _lastAnnouncedGroup = _items[i].Group;
                        changed = false;
                        break;
                    }
                }
            }
            if (_announceFocusAfterRebuild)
            {
                _announceFocusAfterRebuild = false;
                if (changed && Current.UsesListNavigation) AnnounceFocus(includeGroup: true, interrupt: false);
            }
            ApplyVisualFocus();
        }

        private void BuildItems()
        {
            _items.Clear();
            _keys.Clear();
            try
            {
                Current.Build(_items);
            }
            catch (Exception e)
            {
                Log.Error("Context " + Current.Id + " build failed: " + e);
            }
            // Keys must be unique for focus tracking: suffix duplicates.
            var seen = new Dictionary<string, int>();
            foreach (NavItem item in _items)
            {
                string k;
                try { k = item.Key ?? string.Empty; } catch { k = "?"; }
                if (seen.TryGetValue(k, out int n))
                {
                    seen[k] = n + 1;
                    k = k + "#" + (n + 1);
                }
                else
                {
                    seen[k] = 0;
                }
                _keys.Add(k);
            }
        }

        private void HandleListInput()
        {
            bool shift = ModInput.Shift;
            bool ctrl = ModInput.Ctrl;
            if (ModInput.Down(KeyCode.UpArrow)) Move(-1);
            else if (ModInput.Down(KeyCode.DownArrow)) Move(1);
            else if (ModInput.Down(KeyCode.Home)) MoveTo(0);
            else if (ModInput.Down(KeyCode.End)) MoveTo(_items.Count - 1);
            else if (ModInput.Down(KeyCode.Tab)) MoveGroup(shift ? -1 : 1);
            else if (ModInput.Down(KeyCode.PageDown)) MoveGroup(1);
            else if (ModInput.Down(KeyCode.PageUp)) MoveGroup(-1);
            else if (ModInput.Down(KeyCode.LeftArrow)) AdjustOrMove(-1, shift);
            else if (ModInput.Down(KeyCode.RightArrow)) AdjustOrMove(1, shift);
            else if (ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter) || (!Current.ReservesSpace && ModInput.Down(KeyCode.Space)))
            {
                Activate(shift, ctrl);
            }
            else if (ModInput.Down(KeyCode.I) && ModInput.Ctrl)
            {
                NavItem f = Focused;
                string detail = f != null ? SafeString(f.Detail) : null;
                SpeechService.Say(string.IsNullOrEmpty(detail) ? Loc.Get("no_details") : detail, true);
            }
            else if (!ModInput.Ctrl && !ModInput.Alt)
            {
                string typed = UnityEngine.Input.inputString;
                if (!string.IsNullOrEmpty(typed))
                {
                    foreach (char ch in typed)
                    {
                        if (char.IsLetter(ch)) { FirstLetter(ch); break; }
                    }
                }
            }
        }

        /// <summary>Jumps to the next item whose label starts with the typed letter (cycles on repeats).</summary>
        private void FirstLetter(char ch)
        {
            Rebuild(false);
            var labels = new List<string>(_items.Count);
            foreach (NavItem i in _items)
            {
                string l;
                try { l = TextCleaner.Clean(i.Label()); } catch { l = string.Empty; }
                int start = 0;
                while (start < l.Length && !char.IsLetterOrDigit(l[start])) start++;
                labels.Add(l.Substring(start));
            }
            if (_cursor.FindNextByPrefix(ch.ToString(), labels, _keys)) Feedback(MoveResult.Moved);
            else AudioCues.Play(Cue.Edge);
        }

        public void Move(int delta)
        {
            Rebuild(false);
            MoveResult r = _cursor.Move(delta, _keys);
            Feedback(r);
        }

        public void MoveTo(int index)
        {
            Rebuild(false);
            MoveResult r = _cursor.MoveTo(index, _keys);
            Feedback(r == MoveResult.AtEdge ? MoveResult.Moved : r);
        }

        /// <summary>Moves focus without speaking (the caller announces something more useful).</summary>
        public void MoveToSilently(int index)
        {
            _cursor.MoveTo(index, _keys);
            _lastAnnouncedGroup = Focused?.Group;
            ApplyVisualFocus();
        }

        private void MoveGroup(int direction)
        {
            Rebuild(false);
            var groups = new List<string>(_items.Count);
            foreach (NavItem i in _items) groups.Add(i.Group);
            MoveResult r = _cursor.MoveGroup(direction, groups, _keys);
            _lastAnnouncedGroup = null;
            Feedback(r);
        }

        private void AdjustOrMove(int direction, bool large)
        {
            NavItem f = Focused;
            bool handled = false;
            if (f != null)
            {
                try { handled = f.Adjust(direction, large); }
                catch (Exception e) { Log.Error("Adjust failed: " + e); handled = true; }
            }
            if (handled)
            {
                AnnounceFocusIfChanged();
                return;
            }
            Move(direction);
        }

        private void Activate(bool shift, bool ctrl)
        {
            NavItem f = Focused;
            if (f == null)
            {
                SpeechService.Say(Loc.Get("nothing_here"), true);
                return;
            }
            bool handled;
            try
            {
                if (ctrl) handled = f.ActivateThird() || f.Activate();
                else if (shift) handled = f.ActivateAlt() || f.Activate();
                else handled = f.Activate();
            }
            catch (Exception e)
            {
                Log.Error("Activate failed: " + e);
                handled = true;
            }
            if (!handled) SpeechService.Say(f.Speak(), true);
            AudioCues.Play(Cue.Activate);
            AnnounceFocusIfChanged();
        }

        private void Feedback(MoveResult r)
        {
            switch (r)
            {
                case MoveResult.Empty:
                    SpeechService.Say(Loc.Get("nothing_here"), true);
                    return;
                case MoveResult.Wrapped:
                    AudioCues.Play(Cue.Wrap);
                    break;
                case MoveResult.AtEdge:
                    AudioCues.Play(Cue.Edge);
                    break;
                default:
                    AudioCues.Play(Cue.Move);
                    break;
            }
            AnnounceFocus(includeGroup: true, interrupt: true);
            ApplyVisualFocus();
        }

        public void AnnounceFocus(bool includeGroup, bool interrupt)
        {
            NavItem f = Focused;
            if (f == null)
            {
                if (_items.Count == 0 && Current != null && Current.UsesListNavigation) SpeechService.Say(Loc.Get("nothing_here"), interrupt);
                return;
            }
            string text = SafeString(f.Speak);
            string group = f.Group ?? string.Empty;
            if (includeGroup && group.Length > 0 && group != _lastAnnouncedGroup)
            {
                text = TextCleaner.Join(", ", group, text);
            }
            _lastAnnouncedGroup = group;
            int pos = 0, total = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if ((_items[i].Group ?? string.Empty) != group) continue;
                total++;
                if (i <= _cursor.Index) pos++;
            }
            if (total > 1 && Plugin.Settings.AnnouncePosition.Value) text = TextCleaner.Join(", ", text, Loc.Get("position_of", pos, total));
            SpeechService.Say(text, interrupt);
        }

        private void ApplyVisualFocus()
        {
            try
            {
                NavItem f = Focused;
                UiUtil.VisualFocus(f?.Target);
            }
            catch
            {
                // Visual focus is cosmetic.
            }
        }

        private static void SafeExit(ScreenContext c)
        {
            try { c.OnExit(); } catch (Exception e) { Log.Error("Context " + c.Id + " exit failed: " + e); }
        }

        private static string SafeString(Func<string> f)
        {
            try { return f(); } catch (Exception e) { Log.Warn("Text provider failed: " + e.Message); return null; }
        }
    }
}
