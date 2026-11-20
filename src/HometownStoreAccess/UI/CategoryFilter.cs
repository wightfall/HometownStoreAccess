using System;
using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess.UI
{
    /// <summary>
    /// Category filtering for a list screen: Ctrl+Up and Ctrl+Down anywhere on the screen, and a
    /// "Category" item (left and right) at the top. The screen tags each entry with a category path,
    /// calls <see cref="Prepare"/> with all of them and adds only the entries that <see cref="Matches"/>.
    /// The first option always shows everything.
    /// </summary>
    internal sealed class CategoryFilter
    {
        public readonly CategoryCycle Cycle = new CategoryCycle();

        private readonly Func<string, string> _label;
        private readonly Func<NavItem, bool> _isEntry;
        private List<string> _categories = new List<string>();

        /// <summary>Called after the category changed, before the list is rebuilt (to sync the game's own tabs).</summary>
        public Action<string> OnChanged;

        /// <param name="label">Name of a category path; for <see cref="CategoryCycle.All"/> the name of "everything".</param>
        /// <param name="isEntry">Recognises the list entries the filter applies to (focus moves to the first one).</param>
        public CategoryFilter(Func<string, string> label, Func<NavItem, bool> isEntry)
        {
            _label = label;
            _isEntry = isEntry;
        }

        /// <summary>Call at the start of Build with the category of every entry, before filtering.</summary>
        public void Prepare(IEnumerable<string> categoriesOfAllEntries)
        {
            _categories = categoriesOfAllEntries.ToList();
            Cycle.SetOptions(CategoryCycle.OptionsFor(_categories));
        }

        /// <summary>More than one option: worth offering.</summary>
        public bool Active => Cycle.Count > 1;

        public string Current => Cycle.Current;

        public bool Matches(string category) => Cycle.Matches(category);

        public string Label(string option) => _label(option) ?? Loc.Get("cat_All");

        public int CountOf(string option) => CategoryCycle.CountFor(option, _categories);

        /// <summary>"Category, Drinks, 4 items, 3 of 6". Left and right (or Enter) change it.</summary>
        public NavItem Item(string group)
        {
            return new ActionItem(() => Loc.Get("label_category"), () => Step(1, fromItem: true), "category")
            {
                Group = group,
                RoleFunc = () => Loc.Get("role_combobox"),
                ValueFunc = () => Loc.Get("category_value", Label(Cycle.Current), CountOf(Cycle.Current), Cycle.Position, Cycle.Count),
                DetailFunc = () => Loc.Get("hint_category"),
                OnAdjust = (dir, large) =>
                {
                    Step(dir, fromItem: true);
                    return true;
                },
            };
        }

        /// <summary>Ctrl+Up / Ctrl+Down. Returns true when the key was used.</summary>
        public bool HandleKeys()
        {
            if (!ModInput.Ctrl || ModInput.Alt) return false;
            int dir = ModInput.Down(KeyCode.UpArrow) ? -1 : ModInput.Down(KeyCode.DownArrow) ? 1 : 0;
            if (dir == 0) return false;
            if (!Active)
            {
                AudioCues.Play(Cue.Edge);
                SpeechService.Say(Loc.Get("category_none"), true);
                return true;
            }
            Step(dir, fromItem: false);
            return true;
        }

        private void Step(int dir, bool fromItem)
        {
            if (!Active)
            {
                AudioCues.Play(Cue.Edge);
                return;
            }
            string cur = Cycle.Step(dir);
            bool wrapped = dir > 0 ? Cycle.Position == 1 : Cycle.Position == Cycle.Count;
            AudioCues.Play(wrapped ? Cue.Wrap : Cue.Move);
            OnChanged?.Invoke(cur);
            ContextManager cm = AccessibilityRoot.Instance.Contexts;
            cm.Refresh(false);
            string message = Loc.Get("category_count", Label(cur), CountOf(cur));
            if (fromItem)
            {
                SpeechService.Say(message, true);
                return;
            }
            // From the list: continue at the first entry of the new category.
            int first = -1;
            for (int i = 0; i < cm.Items.Count; i++)
            {
                if (_isEntry(cm.Items[i]))
                {
                    first = i;
                    break;
                }
            }
            SpeechService.Say(message, true);
            if (first >= 0)
            {
                cm.MoveToSilently(first);
                cm.AnnounceFocus(includeGroup: false, interrupt: false);
            }
        }
    }
}
