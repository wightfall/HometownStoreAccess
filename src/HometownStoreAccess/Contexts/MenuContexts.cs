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

        private static MainMenuControl Control => Object.FindObjectOfType<MainMenuControl>();

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

        public override void Build(List<NavItem> items)
        {
            GameObject main = MainRoot;
            if (main == null) return;
            UiScan.Add(items, main, string.Empty, includeTexts: false, skip: MenuHelpers.IsLanguageDropdown);
            // Put the language selector after the main buttons, like on screen.
            int insertAt = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Target != null && items[i].Target.name == "Option") { insertAt = i; break; }
            }
            items.Insert(insertAt, new GameLanguageItem());
            Transform aspect = main.transform.parent;
            Transform sound = aspect != null ? aspect.Find("SoundToggle") : null;
            if (sound != null)
            {
                var t = sound.GetComponent<Toggle>();
                if (t != null && t.gameObject.activeInHierarchy) items.Add(new ToggleItem(t, () => Loc.Get("label_mute_music")));
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
            return UIPanelManager.Instance != null ? Loc.Get("ctx_pause_menu") : Loc.Get("ctx_options");
        }

        public override void Build(List<NavItem> items)
        {
            OptionPanel p = Panel;
            if (p == null) return;
            Transform bg = p.transform.Find("BG");
            GameObject root = bg != null ? bg.gameObject : p.gameObject;
            UiScan.Add(items, root, string.Empty, includeTexts: false, skip: go => MenuHelpers.IsLanguageDropdown(go) || go.name == "PAUSED");
            int insertAt = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is SliderItem) { insertAt = i; break; }
                insertAt = i + 1;
            }
            items.Insert(Mathf.Min(insertAt, items.Count), new GameLanguageItem());
            Transform note = root.transform.Find("SaveNotAllowFirstDay");
            if (note != null && note.gameObject.activeInHierarchy)
            {
                UiScan.AddText(items, note.GetComponent<TMP_Text>());
            }
            UiScan.AddText(items, p.versionText);
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
                foreach (SaveSlotPanel p in Object.FindObjectsOfType<SaveSlotPanel>())
                {
                    if (p.gameObject.activeInHierarchy) return p;
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
