using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using UnityEngine;

namespace HometownStoreAccess.Contexts
{
    /// <summary>Which mod overlay is open (key list or settings). Overlays are modal and block game keys.</summary>
    internal static class Overlay
    {
        public enum Kind { None, KeyList, Settings }

        public static Kind Open { get; private set; } = Kind.None;
        public static bool IsOpen => Open != Kind.None;

        private static ScreenContext _underlying;
        private static bool _inGame;

        public static ScreenContext Underlying => _underlying;
        public static bool InGame => _inGame;

        public static void Show(Kind kind, ScreenContext underlying, bool inGame)
        {
            if (Open == Kind.None) _underlying = underlying;
            _inGame = inGame;
            Open = kind;
        }

        public static void Close()
        {
            Open = Kind.None;
            InputGuard.HoldUntilReleased(KeyCode.Escape);
            SpeechService.Say(Loc.Get("help_closed"), true);
        }

        /// <summary>A new scene: the screen under the overlay is gone, so it closes without a word (the scene is said).</summary>
        public static void Reset()
        {
            Open = Kind.None;
            _underlying = null;
            _inGame = false;
        }

        /// <summary>F1: speaks the keys that work on the current screen.</summary>
        public static void SpeakContextHelp(ScreenContext current, bool inGame)
        {
            var parts = new List<string>();
            if (current != null)
            {
                foreach (string l in current.HelpLines()) parts.Add(l.Replace("\n", " "));
            }
            if (current == null || current.UsesListNavigation)
            {
                parts.Add(Loc.Get("help_list_short"));
                // Only where it does something (the list has more than one group) and the screen's own
                // help does not already say it (key names are the same in every language).
                ContextManager cm = AccessibilityRoot.Instance != null ? AccessibilityRoot.Instance.Contexts : null;
                bool told = parts.Exists(p => p.IndexOf("Page Down", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!told && cm != null && cm.Current == current && cm.GroupCount > 1) parts.Add(Loc.Get("help_groups"));
            }
            if (inGame) parts.Add(Loc.Get("help_game_short"));
            // "Shift+F1 lists every key, F5 opens the settings" is not news inside the key list or settings.
            if (!IsOpen) parts.Add(Loc.Get("help_more"));
            SpeechService.Say(TextCleaner.Join(" ", parts.ToArray()), true);
        }
    }

    /// <summary>Shift+F1: every key the mod uses, by section, and a section to listen to each sound cue.</summary>
    internal sealed class KeyListContext : ScreenContext
    {
        public override string Id => "key_list";
        public override int Priority => 1000;
        public override bool HeadingItems => true;
        public override bool ResetFocusOnEnter => true;
        public override bool IsActive() => Overlay.Open == Overlay.Kind.KeyList;
        public override string Title() => Loc.Get("ctx_key_list");

        public override void Build(List<NavItem> items)
        {
            int n = 0;
            void Section(string group, string block)
            {
                if (string.IsNullOrEmpty(block)) return;
                foreach (string line in block.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.Length > 0) items.Add(new TextItem(t, "k" + n++) { Group = group });
                }
            }
            ScreenContext under = Overlay.Underlying;
            if (under != null)
            {
                foreach (string l in under.HelpLines()) Section(Loc.Get("keys_this_screen"), l);
            }
            // With a controller in use, its buttons come first.
            bool pad = Loc.PadMode;
            if (pad) Section(Loc.Get("keys_controller"), Loc.Get("help_controller"));
            Section(Loc.Get("keys_general"), Loc.Get("help_general"));
            Section(Loc.Get("keys_history"), Loc.Get("help_history"));
            if (Overlay.InGame)
            {
                Section(Loc.Get("keys_game"), Loc.Get("help_game_keys"));
                Section(Loc.Get("keys_floor"), Loc.Get("help_floor"));
                Section(Loc.Get("keys_build"), Loc.Get("help_build"));
                Section(Loc.Get("keys_map"), Loc.Get("help_map"));
            }
            if (!pad) Section(Loc.Get("keys_controller"), Loc.Get("help_controller"));
            string sounds = Loc.Get("keys_sounds");
            foreach (Cue cue in Enum.GetValues(typeof(Cue)))
            {
                Cue c = cue;
                items.Add(new ActionItem(() => AudioCues.Name(c), () => AudioCues.Preview(c), "cue:" + c) { Group = sounds, OwnSound = true, DetailFunc = () => Loc.Get("hint_cue_play") });
            }
            items.Add(new ActionItem(() => Loc.Get("label_open_settings"), () => Overlay.Show(Overlay.Kind.Settings, Overlay.Underlying, Overlay.InGame), "open_settings") { Group = Loc.Get("group_actions") });
            items.Add(new ActionItem(() => Loc.Get("label_close"), Overlay.Close, "close") { Group = Loc.Get("group_actions") });
        }

        public override bool HandleInput()
        {
            if (ModInput.Down(KeyCode.Escape) || (ModInput.Down(KeyCode.F1) && ModInput.Shift))
            {
                Overlay.Close();
                return true;
            }
            return false;
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_key_list");
        }
    }

    /// <summary>F5: every mod option, sound cue volumes, and reset to defaults.</summary>
    internal sealed class SettingsContext : ScreenContext
    {
        private float _resetArmedUntil;

        public override string Id => "mod_settings";
        public override int Priority => 1001;
        public override bool HeadingItems => true;
        public override bool IsActive() => Overlay.Open == Overlay.Kind.Settings;
        public override string Title() => Loc.Get("ctx_mod_settings");
        public override string Intro() => Loc.Get("hint_settings");

        public override void Build(List<NavItem> items)
        {
            ModSettings s = Plugin.Settings;
            string speech = Loc.Get("set_group_speech");
            items.Add(new GameLanguageItem { Group = speech });
            items.Add(new ChoiceItem(() => Loc.Get("set_mod_language"), ModLanguageChoices, s.ModLanguage, "modlang") { Group = speech });
            items.Add(new ChoiceItem(() => Loc.Get("set_backend"), BackendChoices, s.SpeechBackend, "backend", onChanged: () => SpeechService.SetPreferred(s.SpeechBackend.Value)) { Group = speech });
            items.Add(Toggle(s.AnnouncePosition, "set_position", speech));
            items.Add(Toggle(s.LogSpeech, "set_log_speech", speech, () => SpeechService.LogSpeech = s.LogSpeech.Value));
            // For bug reports. (The other [Debug] settings, test commands and the game's developer
            // cheats, change the game and stay in the settings file.)
            items.Add(Toggle(s.DebugLogging, "set_verbose", speech, () => Log.DebugEnabled = s.DebugLogging.Value));

            string ann = Loc.Get("set_group_announce");
            items.Add(Toggle(s.AnnounceCustomers, "set_customers", ann));
            items.Add(Toggle(s.AnnounceCustomersLeaving, "set_customers_leaving", ann));
            items.Add(Toggle(s.AnnounceSales, "set_sales", ann));
            items.Add(Toggle(s.AnnounceStock, "set_stock", ann));
            items.Add(Toggle(s.AnnounceCustomerMessages, "set_messages", ann));
            items.Add(Toggle(s.AnnounceCashChanges, "set_cash", ann));
            items.Add(Toggle(s.ReadDialogAutomatically, "set_dialog", ann));
            items.Add(Toggle(s.AnnouncePictures, "set_pictures", ann));
            items.Add(Toggle(s.AnnounceShaking, "set_shaking", ann));
            items.Add(Toggle(s.AnnounceTutorial, "set_tutorial", ann));
            items.Add(Toggle(s.AnnounceAchievements, "set_achievements", ann));
            items.Add(Toggle(s.AnnounceWeather, "set_weather", ann));

            string audio = Loc.Get("set_group_audio");
            items.Add(Toggle(s.AudioCues, "set_cues", audio));
            items.Add(Toggle(s.MenuSounds, "set_menu_sounds", audio));
            items.Add(new VolumeItem(() => Loc.Get("set_cue_master"), s.AudioCueVolume, null) { Group = audio });

            // The volume of each cue is a long list of its own, so Page Down can jump over it.
            string volumes = Loc.Get("set_group_cue_volumes");
            foreach (Cue cue in Enum.GetValues(typeof(Cue)))
            {
                Cue c = cue;
                items.Add(new VolumeItem(() => AudioCues.Name(c), s.CueVolume(c), c) { Group = volumes });
            }

            string controls = Loc.Get("set_group_controls");
            items.Add(Toggle(s.WrapAround, "set_wrap", controls));
            items.Add(Toggle(s.SkipIntro, "set_skip_intro", controls));
            items.Add(Toggle(s.CameraFollowsCursor, "set_camera", controls));
            items.Add(Toggle(s.ControllerEnabled, "set_controller", controls));
            items.Add(Toggle(s.ControllerRumble, "set_rumble", controls));

            string actions = Loc.Get("group_actions");
            items.Add(new ActionItem(() => Loc.Get("set_reset"), ResetAll, "reset") { Group = actions, DetailFunc = () => Loc.Get("hint_reset") });
            items.Add(new ActionItem(() => Loc.Get("label_close"), Overlay.Close, "close") { Group = actions });
        }

        private static NavItem Toggle(ConfigEntry<bool> entry, string key, string group, Action onChanged = null)
        {
            return new ActionItem(() => Loc.Get(key), () =>
            {
                entry.Value = !entry.Value;
                onChanged?.Invoke();
                SpeechService.Say(entry.Value ? Loc.Get("state_on") : Loc.Get("state_off"), true);
            }, "cfg:" + key)
            {
                Group = group,
                ValueFunc = () => entry.Value ? Loc.Get("state_on") : Loc.Get("state_off"),
                RoleFunc = () => Loc.Get("role_checkbox"),
                OnAdjust = (dir, large) =>
                {
                    entry.Value = dir > 0;
                    onChanged?.Invoke();
                    SpeechService.Say(entry.Value ? Loc.Get("state_on") : Loc.Get("state_off"), true);
                    return true;
                },
            };
        }

        private static List<KeyValuePair<string, string>> ModLanguageChoices()
        {
            var list = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Auto", Loc.Get("set_auto")) };
            foreach (string l in Loc.AvailableLanguages) list.Add(new KeyValuePair<string, string>(l, LanguageFiles.DisplayName(l)));
            return list;
        }

        private static List<KeyValuePair<string, string>> BackendChoices()
        {
            var list = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Auto", Loc.Get("set_auto")) };
            foreach (string b in SpeechService.BackendNames()) list.Add(new KeyValuePair<string, string>(b, b));
            return list;
        }

        private void ResetAll()
        {
            if (Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = Time.unscaledTime + 5f;
                SpeechService.Say(Loc.Get("set_reset_confirm"), true);
                return;
            }
            _resetArmedUntil = 0f;
            bool hadGameLanguage = GameLanguage.Overridden;
            Plugin.Settings.ResetToDefaults();
            SpeechService.LogSpeech = Plugin.Settings.LogSpeech.Value;
            Log.DebugEnabled = Plugin.Settings.DebugLogging.Value;
            SpeechService.SetPreferred(Plugin.Settings.SpeechBackend.Value);
            if (hadGameLanguage) GameLanguage.BackToGameLanguage();
            GameLanguage.SyncModLanguage();
            SpeechService.Say(Loc.Get("set_reset_done"), true);
        }

        public override bool HandleInput()
        {
            if (ModInput.Down(KeyCode.Escape) || ModInput.Down(KeyCode.F5))
            {
                Overlay.Close();
                return true;
            }
            return false;
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("hint_settings");
        }
    }

    /// <summary>Choose one of several values of a string setting with left/right or Enter.</summary>
    internal sealed class ChoiceItem : NavItem
    {
        private readonly Func<string> _label;
        private readonly Func<List<KeyValuePair<string, string>>> _choices;
        private readonly ConfigEntry<string> _entry;
        private readonly string _key;
        private readonly Action _onChanged;

        public ChoiceItem(Func<string> label, Func<List<KeyValuePair<string, string>>> choices, ConfigEntry<string> entry, string key, Action onChanged = null)
        {
            _label = label;
            _choices = choices;
            _entry = entry;
            _key = key;
            _onChanged = onChanged;
        }

        public override string Key => "choice:" + _key;
        public override string Label() => _label();
        public override string Role() => Loc.Get("role_combobox");

        public override string Value()
        {
            var c = _choices();
            int i = Index(c);
            return i < 0 ? _entry.Value : c[i].Value + " (" + (i + 1) + "/" + c.Count + ")";
        }

        private int Index(List<KeyValuePair<string, string>> c) => c.FindIndex(kv => string.Equals(kv.Key, _entry.Value, StringComparison.OrdinalIgnoreCase));

        /// <summary>Left and right: the previous or next value (round the ends when wrapping is on).</summary>
        public override bool Adjust(int direction, bool large) => Step(direction, NavSettings.Wrap);

        /// <summary>Enter: the next value, always going round, so one key reaches every value.</summary>
        public override bool Activate() => Step(1, wrap: true);

        private bool Step(int direction, bool wrap)
        {
            var c = _choices();
            if (c.Count == 0) return true;
            int i = ChoiceStep.Step(Index(c), c.Count, direction, wrap, out bool atEdge);
            if (atEdge)
            {
                AudioCues.Play(Cue.Edge);
                SpeechService.Say(Value(), true);
                return true;
            }
            _entry.Value = c[i].Key;
            _onChanged?.Invoke();
            if (_entry.Definition.Key == "ModLanguage") GameLanguage.SyncModLanguage();
            SpeechService.Say(Value(), true);
            return true;
        }
    }

    /// <summary>A 0-100% volume setting; with a cue, the cue plays at the new volume.</summary>
    internal sealed class VolumeItem : NavItem
    {
        private readonly Func<string> _label;
        private readonly ConfigEntry<float> _entry;
        private readonly Cue? _cue;

        public VolumeItem(Func<string> label, ConfigEntry<float> entry, Cue? cue)
        {
            _label = label;
            _entry = entry;
            _cue = cue;
        }

        public override string Key => "vol:" + _entry.Definition.Key;
        public override string Label() => _label();
        public override string Value() => Mathf.RoundToInt(_entry.Value * 100f) + "%";
        public override string Role() => Loc.Get("role_slider");
        public override bool PlaysOwnSound => true;

        public override bool Adjust(int direction, bool large)
        {
            float step = large ? 0.25f : 0.1f;
            _entry.Value = Mathf.Clamp01(Mathf.Round((_entry.Value + direction * step) * 100f) / 100f);
            SpeechService.Say(Value(), true);
            AudioCues.Preview(_cue ?? Cue.Activate);
            return true;
        }

        public override bool Activate()
        {
            AudioCues.Preview(_cue ?? Cue.Activate);
            return true;
        }
    }
}

namespace HometownStoreAccess.Contexts
{
    /// <summary>
    /// Prevents the game from reacting to keys the mod handled itself (Escape that closed an overlay,
    /// any key while an overlay is open).
    /// </summary>
    internal static class InputGuard
    {
        private static readonly System.Collections.Generic.HashSet<UnityEngine.KeyCode> Held = new System.Collections.Generic.HashSet<UnityEngine.KeyCode>();

        public static bool BlockGame => Overlay.IsOpen || Held.Count > 0;

        public static void HoldUntilReleased(UnityEngine.KeyCode key) => Held.Add(key);

        public static void LateUpdate()
        {
            if (Held.Count == 0) return;
            var release = new System.Collections.Generic.List<UnityEngine.KeyCode>();
            foreach (UnityEngine.KeyCode k in Held)
            {
                if (!UnityEngine.Input.GetKey(k) && !UnityEngine.Input.GetKeyUp(k) && !UnityEngine.Input.GetKeyDown(k)) release.Add(k);
            }
            foreach (UnityEngine.KeyCode k in release) Held.Remove(k);
        }
    }
}
