using System;
using HometownStoreAccess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.UI
{
    /// <summary>Navigation preferences shared by the lists and the value pickers.</summary>
    internal static class NavSettings
    {
        /// <summary>Lists and choices go round at their ends (mod setting "Wrap around").</summary>
        public static bool Wrap => Plugin.Settings == null || Plugin.Settings.WrapAround.Value;
    }

    /// <summary>One focusable entry in a screen's navigation list.</summary>
    internal abstract class NavItem
    {
        /// <summary>Section name; spoken when focus enters a new section.</summary>
        public string Group = string.Empty;

        /// <summary>Stable identity so focus survives list rebuilds.</summary>
        public virtual string Key => GetType().Name + ":" + Label();

        public abstract string Label();
        public virtual string Value() => null;
        public virtual string Role() => null;
        public virtual string Detail() => null;
        public virtual bool Enabled => true;
        public virtual GameObject Target => null;

        /// <summary>Enter.</summary>
        public virtual bool Activate() => false;
        /// <summary>Shift+Enter (controller X).</summary>
        public virtual bool ActivateAlt() => false;
        /// <summary>Ctrl+Enter.</summary>
        public virtual bool ActivateThird() => false;
        /// <summary>Left/Right; return false to let the list move instead.</summary>
        public virtual bool Adjust(int direction, bool large) => false;

        public string Speak()
        {
            string role = Role();
            string value = Value();
            string text = TextCleaner.Join(", ", Label(), value, role);
            if (!Enabled) text = TextCleaner.Join(", ", text, Loc.Get("state_unavailable"));
            return text;
        }
    }

    /// <summary>A line of information (not interactive).</summary>
    internal sealed class TextItem : NavItem
    {
        private readonly Func<string> _text;
        private readonly string _key;
        private readonly Func<string> _detail;

        public TextItem(string text, string key = null) : this(() => text, key) { }

        public TextItem(Func<string> text, string key = null, Func<string> detail = null)
        {
            _text = text;
            _key = key;
            _detail = detail;
        }

        public override string Key => _key ?? ("text:" + Label());
        public override string Label() => _text() ?? string.Empty;
        public override string Detail() => _detail?.Invoke();
    }

    /// <summary>A custom action with optional alternates and adjust handler.</summary>
    internal sealed class ActionItem : NavItem
    {
        private readonly Func<string> _label;
        private readonly string _key;
        public Func<string> ValueFunc;
        public Func<string> RoleFunc;
        public Func<string> DetailFunc;
        public Action OnActivate;
        public Action OnActivateAlt;
        public Action OnActivateThird;
        public Func<int, bool, bool> OnAdjust;
        public GameObject TargetObject;

        public ActionItem(Func<string> label, Action onActivate, string key = null)
        {
            _label = label;
            OnActivate = onActivate;
            _key = key;
        }

        public ActionItem(string label, Action onActivate, string key = null) : this(() => label, onActivate, key) { }

        public override string Key => _key ?? ("action:" + Label());
        public override string Label() => _label() ?? string.Empty;
        public override string Value() => ValueFunc?.Invoke();
        public override string Role() => RoleFunc?.Invoke();
        public override string Detail() => DetailFunc?.Invoke();
        public override GameObject Target => TargetObject;

        public override bool Activate()
        {
            if (OnActivate == null) return false;
            if (!Enabled)
            {
                SpeechSay(Loc.Get("state_unavailable"));
                return true;
            }
            OnActivate();
            return true;
        }

        public override bool ActivateAlt()
        {
            if (OnActivateAlt == null) return false;
            OnActivateAlt();
            return true;
        }

        public override bool ActivateThird()
        {
            if (OnActivateThird == null) return false;
            OnActivateThird();
            return true;
        }

        public override bool Adjust(int direction, bool large) => OnAdjust != null && OnAdjust(direction, large);

        private static void SpeechSay(string s) => Speech.SpeechService.Say(s);
    }

    /// <summary>Wraps a Unity UI Button (or any clickable Selectable).</summary>
    internal sealed class ButtonItem : NavItem
    {
        private readonly Selectable _sel;
        private readonly Func<string> _label;
        private readonly string _key;
        public Func<string> DetailFunc;
        public Func<string> ValueFunc;

        public ButtonItem(Selectable sel, Func<string> label = null, string key = null)
        {
            _sel = sel;
            _label = label;
            _key = key;
        }

        public override string Key => _key ?? ("btn:" + (_sel != null ? _sel.GetInstanceID().ToString() : Label()));
        public override string Label() => _label != null ? _label() : UiUtil.LabelFor(_sel != null ? _sel.gameObject : null);
        public override string Value() => ValueFunc?.Invoke();
        public override string Detail() => DetailFunc?.Invoke();
        public override bool Enabled => _sel != null && _sel.IsInteractable();
        public override GameObject Target => _sel != null ? _sel.gameObject : null;

        public override bool Activate()
        {
            if (_sel == null) return false;
            if (!_sel.IsInteractable())
            {
                Speech.SpeechService.Say(Loc.Get("state_unavailable"));
                return true;
            }
            UiUtil.Click(_sel.gameObject);
            return true;
        }
    }

    internal sealed class ToggleItem : NavItem
    {
        private readonly Toggle _toggle;
        private readonly Func<string> _label;

        public ToggleItem(Toggle toggle, Func<string> label = null)
        {
            _toggle = toggle;
            _label = label;
        }

        public override string Key => "tgl:" + _toggle.GetInstanceID();
        public override string Label() => _label != null ? _label() : UiUtil.LabelFor(_toggle.gameObject);
        public override string Value() => _toggle.isOn ? Loc.Get("state_checked") : Loc.Get("state_unchecked");
        public override string Role() => Loc.Get("role_checkbox");
        public override bool Enabled => _toggle.IsInteractable();
        public override GameObject Target => _toggle.gameObject;

        public override bool Activate()
        {
            if (!_toggle.IsInteractable())
            {
                Speech.SpeechService.Say(Loc.Get("state_unavailable"));
                return true;
            }
            UiUtil.Click(_toggle.gameObject);
            Speech.SpeechService.Say(Value());
            return true;
        }
    }

    internal sealed class SliderItem : NavItem
    {
        private readonly Slider _slider;
        private readonly Func<string> _label;
        private readonly float _step;

        public SliderItem(Slider slider, Func<string> label = null, float step = 0f)
        {
            _slider = slider;
            _label = label;
            _step = step;
        }

        public override string Key => "sld:" + _slider.GetInstanceID();
        public override string Label() => _label != null ? _label() : UiUtil.LabelFor(_slider.gameObject);
        public override string Value() => UiUtil.Percent(_slider);
        public override string Role() => Loc.Get("role_slider");
        public override bool Enabled => _slider.IsInteractable();
        public override GameObject Target => _slider.gameObject;

        public override bool Adjust(int direction, bool large)
        {
            if (!_slider.IsInteractable()) return true;
            float range = _slider.maxValue - _slider.minValue;
            float step = _step > 0f ? _step : (_slider.wholeNumbers ? 1f : range / 20f);
            if (large) step *= _slider.wholeNumbers ? 10f : 4f;
            float v = Mathf.Clamp(_slider.value + direction * step, _slider.minValue, _slider.maxValue);
            // Snap near the ends so 0% and 100% are reachable despite float steps.
            if (Mathf.Abs(v - _slider.minValue) < step * 0.5f) v = _slider.minValue;
            if (Mathf.Abs(v - _slider.maxValue) < step * 0.5f) v = _slider.maxValue;
            _slider.value = v;
            Speech.SpeechService.Say(Value());
            return true;
        }

        public override bool Activate()
        {
            Speech.SpeechService.Say(Loc.Get("hint_slider"));
            return true;
        }
    }

    internal sealed class DropdownItem : NavItem
    {
        private readonly TMP_Dropdown _dropdown;
        private readonly Func<string> _label;

        public DropdownItem(TMP_Dropdown dropdown, Func<string> label = null)
        {
            _dropdown = dropdown;
            _label = label;
        }

        public override string Key => "dd:" + _dropdown.GetInstanceID();
        public override string Label() => _label != null ? _label() : LabelOverrides.Find(_dropdown.gameObject) ?? TextCleaner.HumanizeName(_dropdown.gameObject.name);
        public override string Value()
        {
            if (_dropdown.options == null || _dropdown.options.Count == 0) return string.Empty;
            int i = Mathf.Clamp(_dropdown.value, 0, _dropdown.options.Count - 1);
            return TextCleaner.Clean(_dropdown.options[i].text) + " (" + (i + 1) + "/" + _dropdown.options.Count + ")";
        }
        public override string Role() => Loc.Get("role_combobox");
        public override bool Enabled => _dropdown.IsInteractable();
        public override GameObject Target => _dropdown.gameObject;

        /// <summary>Left and right: the previous or next option (round the ends when wrapping is on).</summary>
        public override bool Adjust(int direction, bool large) => Step(direction, NavSettings.Wrap);

        /// <summary>Enter: the next option, always going round.</summary>
        public override bool Activate() => Step(1, wrap: true);

        private bool Step(int direction, bool wrap)
        {
            if (!_dropdown.IsInteractable() || _dropdown.options.Count == 0) return true;
            int next = ChoiceStep.Step(_dropdown.value, _dropdown.options.Count, direction, wrap, out bool atEdge);
            if (atEdge) Audio.AudioCues.Play(Audio.Cue.Edge);
            else if (next != _dropdown.value) _dropdown.value = next;
            Speech.SpeechService.Say(Value());
            return true;
        }
    }

    internal sealed class InputItem : NavItem
    {
        private readonly TMP_InputField _field;
        private readonly Func<string> _label;

        public InputItem(TMP_InputField field, Func<string> label = null)
        {
            _field = field;
            _label = label;
        }

        public TMP_InputField Field => _field;
        public override string Key => "in:" + _field.GetInstanceID();
        public override string Label()
        {
            if (_label != null) return _label();
            string over = LabelOverrides.Find(_field.gameObject);
            if (!string.IsNullOrEmpty(over)) return over;
            string placeholder = _field.placeholder is TMP_Text ph ? TextCleaner.Clean(ph.text) : string.Empty;
            return placeholder.Length > 0 ? placeholder.TrimEnd('.', ' ') : TextCleaner.HumanizeName(_field.gameObject.name);
        }
        public override string Value()
        {
            string t = TextCleaner.Clean(_field.text);
            return t.Length == 0 ? Loc.Get("state_empty") : t;
        }
        public override string Role() => Loc.Get("role_edit");
        public override bool Enabled => _field.IsInteractable();
        public override GameObject Target => _field.gameObject;

        public override bool Activate()
        {
            if (!_field.IsInteractable()) return true;
            TextEntry.Begin(_field);
            return true;
        }
    }
}
