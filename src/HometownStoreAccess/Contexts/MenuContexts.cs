using System.Collections.Generic;
using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    internal static class MenuHelpers
    {
        public static GameObject Ancestor(Component c, string name)
        {
            if (c == null) return null;
            Transform t = c.transform;
            while (t != null)
            {
                if (t.name == name) return t.gameObject;
                t = t.parent;
            }
            return null;
        }

        public static bool IsLanguageDropdown(GameObject go) => go.GetComponent<LanguageDropdown>() != null;
    }

    /// <summary>The title screen: Continue, New game, Load, Exit, language, options, credits, Discord, music.</summary>
    internal sealed class MainMenuContext : ScreenContext
    {
        public override string Id => "main_menu";
        public override int Priority => 10;

        private static MainMenuControl Control => SceneCache.Get<MainMenuControl>();

        private GameObject MainRoot
        {
            get
            {
                MainMenuControl c = Control;
                return c != null ? MenuHelpers.Ancestor(c.NewGameButton, "Main") : null;
            }
        }

        public override GameObject Root => MainRoot;

        public override bool IsActive()
        {
            GameObject main = MainRoot;
            return main != null && main.activeInHierarchy;
        }

        public override string Title() => Loc.Get("ctx_main_menu");

        /// <summary>
        /// Two groups for Page Up and Page Down: the game buttons (Continue, New game, Load game, Exit)
        /// without a name, then the language, options, credits, Discord and the music switch.
        /// </summary>
        public override void Build(List<NavItem> items)
        {
            GameObject main = MainRoot;
            if (main == null) return;
            string more = Loc.Get("group_menu_more");
            Transform buttons = main.transform.Find("Scroll View");
            int firstMore;
            if (buttons != null)
            {
                UiScan.Add(items, buttons.gameObject, string.Empty);
                // Neither asks first: Exit closes the game at once (unlike Quit in the pause menu), and a
                // new game's autosave (from its second day) replaces the one Continue loads.
                foreach (NavItem item in items)
                {
                    if (!(item is ButtonItem b) || b.Target == null) continue;
                    if (b.Target.name == "Exit") b.ValueFunc = () => Loc.Get("menu_exit_value");
                    else if (b.Target.name == "New Game") b.ValueFunc = () => Loc.Get("menu_new_game_value");
                }
                firstMore = items.Count;
                UiScan.Add(items, main, more, skip: go => go.transform == buttons || MenuHelpers.IsLanguageDropdown(go));
            }
            else
            {
                // Unknown layout (a later game version): one list, the language before Options.
                UiScan.Add(items, main, string.Empty, skip: MenuHelpers.IsLanguageDropdown);
                firstMore = items.Count;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Target != null && items[i].Target.name == "Option") { firstMore = i; break; }
                }
                more = string.Empty;
            }
            items.Insert(firstMore, new GameLanguageItem { Group = more });
            Transform aspect = main.transform.parent;
            Transform sound = aspect != null ? aspect.Find("SoundToggle") : null;
            if (sound != null)
            {
                var t = sound.GetComponent<Toggle>();
                if (t != null && t.gameObject.activeInHierarchy) items.Add(new ToggleItem(t, () => Loc.Get("label_mute_music")) { Group = more });
            }
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_menu_lines");
        }
    }

    /// <summary>The options / pause panel (both scenes).</summary>
    internal sealed class OptionsContext : ScreenContext
    {
        public override string Id => "options";
        public override int Priority => 90;

        private static OptionPanel Panel => GlobeOption.Instance != null ? GlobeOption.Instance.optionPanel : null;

        public override GameObject Root => Panel != null ? Panel.gameObject : null;

        public override bool IsActive()
        {
            OptionPanel p = Panel;
            return p != null && p.gameObject.activeInHierarchy && !(p.saveSlotPanel != null && p.saveSlotPanel.gameObject.activeInHierarchy);
        }

        public override string Title()
        {
            return AccessibilityRoot.Instance != null && AccessibilityRoot.Instance.InGame ? Loc.Get("ctx_pause_menu") : Loc.Get("ctx_options");
        }

        /// <summary>
        /// Groups for Page Up and Page Down, as the panel is laid out: Close, then General (full screen,
        /// show info, language), Sound (the volumes), Game (save, return to the main menu, quit; only
        /// in the game) and About (the version).
        /// </summary>
        public override void Build(List<NavItem> items)
        {
            OptionPanel p = Panel;
            if (p == null) return;
            Transform bg = p.transform.Find("BG");
            GameObject root = bg != null ? bg.gameObject : p.gameObject;
            Transform general = root.transform.Find("OptionBG");
            Transform sound = root.transform.Find("SondBG") ?? root.transform.Find("SoundBG");
            Transform footer = root.transform.Find("Footer");
            Transform close = root.transform.Find("CloseButton");
            Transform note = root.transform.Find("SaveNotAllowFirstDay");
            if (general == null || sound == null)
            {
                BuildFlat(items, p, root, note);
                return;
            }
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>());
            string g = Loc.Get("group_options_general");
            UiScan.Add(items, general.gameObject, g, skip: MenuHelpers.IsLanguageDropdown);
            items.Add(new GameLanguageItem { Group = g });
            UiScan.Add(items, sound.gameObject, Loc.Get("group_options_sound"));
            string game = Loc.Get("group_options_game");
            if (footer != null) UiScan.Add(items, footer.gameObject, game);
            if (note != null && note.gameObject.activeInHierarchy) UiScan.AddText(items, note.GetComponent<TMP_Text>(), game);
            // Anything else the panel shows (added by a later game version) is not left out.
            var known = new HashSet<Transform> { general, sound, footer, close, note };
            UiScan.Add(items, root, string.Empty, skip: go => known.Contains(go.transform) || MenuHelpers.IsLanguageDropdown(go) || go.name == "PAUSED");
            UiScan.AddText(items, p.versionText, Loc.Get("group_options_about"));
        }

        private static void BuildFlat(List<NavItem> items, OptionPanel p, GameObject root, Transform note)
        {
            UiScan.Add(items, root, string.Empty, includeTexts: false, skip: go => MenuHelpers.IsLanguageDropdown(go) || go.name == "PAUSED");
            int insertAt = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is SliderItem) { insertAt = i; break; }
                insertAt = i + 1;
            }
            items.Insert(Mathf.Min(insertAt, items.Count), new GameLanguageItem());
            if (note != null && note.gameObject.activeInHierarchy) UiScan.AddText(items, note.GetComponent<TMP_Text>());
            UiScan.AddText(items, p.versionText);
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_options");
        }
    }

    /// <summary>Save / load slot list, including the new-save name prompt.</summary>
    internal sealed class SaveSlotContext : ScreenContext
    {
        public override string Id => "save_slots";
        public override int Priority => 94;

        private static SaveSlotPanel Panel
        {
            get
            {
                foreach (SaveSlotPanel p in SceneCache.All<SaveSlotPanel>())
                {
                    if (p != null && p.gameObject.activeInHierarchy) return p;
                }
                return null;
            }
        }

        public override GameObject Root => Panel != null ? Panel.gameObject : null;
        public override bool IsActive() => Panel != null;

        public override string Title()
        {
            SaveSlotPanel p = Panel;
            string head = p != null ? UiUtil.Text(p.headerTitle) : string.Empty;
            return head.Length > 0 ? head : Loc.Get("ctx_save_slots");
        }

        public override void Build(List<NavItem> items)
        {
            SaveSlotPanel p = Panel;
            if (p == null) return;
            if (p.inputNamePanel != null && p.inputNamePanel.activeInHierarchy)
            {
                string g = Loc.Get("group_save_name");
                UiScan.Add(items, p.inputNamePanel, g, includeTexts: true);
                return;
            }
            string slots = Loc.Get("group_saves");
            foreach (Transform child in p.saveSlotParent)
            {
                var slot = child.GetComponent<ContentSaveSlot>();
                if (slot == null || !child.gameObject.activeInHierarchy) continue;
                if (slot.newSaveSlot != null && slot.newSaveSlot.activeSelf)
                {
                    items.Add(new ButtonItem(slot.newSaveButton, () => Loc.Get("label_new_save"), "newsave") { Group = slots });
                    continue;
                }
                ContentSaveSlot s = slot;
                var item = new ActionItem(() => SlotLabel(s), () => UiUtil.Click(s.normalSaveButton.gameObject), "slot:" + slot.GetInstanceID())
                {
                    Group = slots,
                    DetailFunc = () => Loc.Get("hint_save_slot"),
                    TargetObject = slot.normalSaveButton.gameObject,
                    OnActivateAlt = () => TextEntry.Begin(s.titleSaveNameText),
                    OnActivateThird = () => UiUtil.Click(s.deleteSaveButton.gameObject),
                };
                items.Add(item);
            }
            if (p.warningBetweenPhase != null && p.warningBetweenPhase.activeInHierarchy)
            {
                var t = p.warningBetweenPhase.GetComponent<TMP_Text>();
                if (t != null) UiScan.AddText(items, t);
            }
            Transform close = p.transform.Find("CloseButton");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>());
        }

        public override bool HandleInput()
        {
            if (ModInput.Down(KeyCode.Delete))
            {
                SaveSlotPanel p = Panel;
                if (p == null) return false;
                // Delete the focused slot (same as Ctrl+Enter).
                var focused = AccessibilityRoot.Instance.Contexts.Focused;
                return focused != null && focused.ActivateThird();
            }
            return false;
        }

        private static string SlotLabel(ContentSaveSlot s)
        {
            string name = s.titleSaveNameText != null ? TextCleaner.Clean(s.titleSaveNameText.text) : "?";
            return TextCleaner.Join(", ", name, UiUtil.Text(s.dateSaveText), Loc.Get("save_money", UiUtil.Text(s.moneyText)), Loc.Get("save_days", UiUtil.Text(s.dayPlayedText)), UiUtil.Text(s.versionText));
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("hint_save_slot");
        }
    }
}
