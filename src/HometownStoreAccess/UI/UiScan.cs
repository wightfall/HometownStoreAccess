using System;
using System.Collections.Generic;
using HometownStoreAccess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.UI
{
    /// <summary>Generic discovery of controls and text lines under a UI root, in visual (hierarchy) order.</summary>
    internal static class UiScan
    {
        /// <summary>
        /// Adds every shown, supported Selectable under <paramref name="root"/> (buttons, toggles,
        /// sliders, dropdowns, input fields). Scrollbars and dropdown templates are skipped.
        /// When <paramref name="includeTexts"/> is set, standalone text lines are added in order too.
        /// </summary>
        public static void Add(List<NavItem> items, GameObject root, string group = "", bool includeTexts = false, Func<GameObject, bool> skip = null)
        {
            if (root == null) return;
            Walk(items, root.transform, group, includeTexts, skip, insideSelectable: false);
        }

        private static void Walk(List<NavItem> items, Transform t, string group, bool includeTexts, Func<GameObject, bool> skip, bool insideSelectable)
        {
            GameObject go = t.gameObject;
            if (!go.activeInHierarchy) return;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha <= 0.01f) return;
            if (skip != null && skip(go)) return;
            if (go.name == "Template" && go.GetComponentInParent<TMP_Dropdown>() != null) return;

            NavItem item = null;
            bool isSelectable = false;
            var sel = go.GetComponent<Selectable>();
            if (sel != null && !(sel is Scrollbar))
            {
                isSelectable = true;
                if (sel is Toggle toggle) item = new ToggleItem(toggle);
                else if (sel is Slider slider) item = new SliderItem(slider);
                else if (sel is TMP_Dropdown dd) item = new DropdownItem(dd);
                else if (sel is TMP_InputField input) item = new InputItem(input);
                else item = new ButtonItem(sel);
            }
            else if (includeTexts && !insideSelectable)
            {
                var tmp = go.GetComponent<TMP_Text>();
                if (tmp != null && tmp.enabled && go.GetComponentInParent<TMP_InputField>() == null)
                {
                    string s = TextCleaner.Clean(tmp.text);
                    if (!TextCleaner.IsBlank(s))
                    {
                        TMP_Text captured = tmp;
                        item = new TextItem(() => TextCleaner.Clean(captured.text), "txt:" + tmp.GetInstanceID());
                    }
                }
            }
            if (item != null)
            {
                item.Group = group;
                items.Add(item);
            }
            // Children of a selectable are its label/graphics; do not add them separately.
            if (isSelectable && !(sel is TMP_InputField)) return;
            if (sel is TMP_InputField) return;
            for (int i = 0; i < t.childCount; i++) Walk(items, t.GetChild(i), group, includeTexts, skip, insideSelectable || isSelectable);
        }

        /// <summary>Adds a single control if it is shown.</summary>
        public static NavItem AddControl(List<NavItem> items, Selectable sel, string group = "", Func<string> label = null)
        {
            if (sel == null || !UiUtil.IsShown(sel.gameObject)) return null;
            NavItem item;
            if (sel is Toggle toggle) item = new ToggleItem(toggle, label);
            else if (sel is Slider slider) item = new SliderItem(slider, label);
            else if (sel is TMP_Dropdown dd) item = new DropdownItem(dd, label);
            else if (sel is TMP_InputField input) item = new InputItem(input, label);
            else item = new ButtonItem(sel, label);
            item.Group = group;
            items.Add(item);
            return item;
        }

        /// <summary>Adds a text line if the text component is shown and not blank.</summary>
        public static NavItem AddText(List<NavItem> items, TMP_Text text, string group = "", Func<string, string> format = null)
        {
            if (text == null || !UiUtil.IsShown(text.gameObject)) return null;
            string s = TextCleaner.Clean(text.text);
            if (TextCleaner.IsBlank(s)) return null;
            TMP_Text captured = text;
            var item = new TextItem(() =>
            {
                string v = TextCleaner.Clean(captured.text);
                return format != null ? format(v) : v;
            }, "txt:" + text.GetInstanceID()) { Group = group };
            items.Add(item);
            return item;
        }
    }
}
