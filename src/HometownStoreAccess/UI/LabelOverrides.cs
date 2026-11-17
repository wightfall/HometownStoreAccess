using System.Collections.Generic;
using HometownStoreAccess.Core;
using UnityEngine;

namespace HometownStoreAccess.UI
{
    /// <summary>
    /// Mod-provided labels for the game's icon-only controls, keyed by GameObject name
    /// (optionally "Parent/Name"). Values are localization keys.
    /// </summary>
    internal static class LabelOverrides
    {
        private static readonly Dictionary<string, string> ByParentAndName = new Dictionary<string, string>
        {
            ["MainButton/Summary"] = "label_daily_summary",
            ["MainButton (1)/Summary"] = "label_daily_summary",
            ["MainButton/Event"] = "label_event_journal",
            ["MainButton (1)/Event"] = "label_event_journal",
            ["MainButton/Customer"] = "label_customers",
            ["MainButton (1)/Customer"] = "label_customers",
            ["TransferPanel/Button"] = "label_close",
            ["ShowSkin/ConfirmButton"] = "label_confirm_skin",
            ["Main/Language"] = "label_language",
            ["OptionBG/Language"] = "label_language",
        };

        private static readonly Dictionary<string, string> ByName = new Dictionary<string, string>
        {
            ["CloseButton"] = "label_close",
            ["Close"] = "label_close",
            ["Close Button"] = "label_close",
            ["Close Panel"] = "label_close",
            ["ClosePanelButton"] = "label_close",
            ["Close Log Button"] = "label_close",
            ["CloseMap"] = "label_close_map",
            ["TC_Stop"] = "label_pause",
            ["TC_x1"] = "label_speed1",
            ["TC_x2"] = "label_speed2",
            ["TC_x3"] = "label_speed3",
            ["SettingButton"] = "label_settings",
            ["SettingButton (Need To Set on Screen)"] = "label_settings",
            ["SoundToggle"] = "label_mute_music",
            ["Delete"] = "label_delete_save",
            ["LogButton"] = "label_dialog_log",
            ["NextPhaseButton"] = "label_next_phase",
            ["Toggle DevTool"] = "label_dev_tools",
            ["Discord"] = "label_discord",
            ["SearchInput"] = "label_search",
            ["Minus10Button"] = "label_minus10",
            ["MinusButton"] = "label_minus1",
            ["PlusButton"] = "label_plus1",
            ["Plus10Button"] = "label_plus10",
            ["Letter"] = "label_open_letter",
        };

        /// <summary>Returns the localized override label, or null when the game's own text should be used.</summary>
        public static string Find(GameObject go)
        {
            if (go == null) return null;
            Transform parent = go.transform.parent;
            if (parent != null && ByParentAndName.TryGetValue(parent.name + "/" + go.name, out string pk)) return Loc.Get(pk);
            if (ByName.TryGetValue(go.name, out string key)) return Loc.Get(key);
            return null;
        }
    }
}
