using System.Linq;
using System.Text.RegularExpressions;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>
    /// With a controller in use (Loc.PadMode), texts that have a controller version ("key.pad") name the
    /// controller's buttons; the keyboard version is used otherwise (1.4.0).
    /// </summary>
    public class PadModeTests
    {
        [Fact]
        public void PadModeGivesTheControllerVersion()
        {
            Loc.SetLanguage("en");
            try
            {
                Loc.PadMode = false;
                Assert.Contains("Enter", Loc.Get("hint_open_building"));
                Loc.PadMode = true;
                Assert.Equal("Press A to open it.", Loc.Get("hint_open_building"));
                Assert.Contains("RB", Loc.Get("tg_build"));
            }
            finally
            {
                Loc.PadMode = false;
            }
        }

        [Fact]
        public void TextsWithoutAControllerVersionStayTheSame()
        {
            Loc.SetLanguage("en");
            try
            {
                string keyboard = Loc.Get("walk_off");
                Loc.PadMode = true;
                Assert.Equal(keyboard, Loc.Get("walk_off"));
                Assert.Equal("walk_off", Loc.EffectiveKey("walk_off"));
                Assert.Equal("tg_build.pad", Loc.EffectiveKey("tg_build"));
            }
            finally
            {
                Loc.PadMode = false;
            }
        }

        [Fact]
        public void PlaceholdersAreFilledInBothModes()
        {
            Loc.SetLanguage("th");
            try
            {
                Loc.PadMode = true;
                Assert.Contains("ชั้นวาง", Loc.Get("placing_start", "ชั้นวาง", 2));
                Assert.Contains("2", Loc.Get("placing_start", "ชั้นวาง", 2));
                Assert.Contains("3", Loc.Get("phase_morning_start", 3));
            }
            finally
            {
                Loc.PadMode = false;
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void EveryControllerVersionHasAKeyboardVersion()
        {
            foreach (string key in Loc.AllKeys.Where(k => k.EndsWith(Loc.PadSuffix)))
            {
                string baseKey = key.Substring(0, key.Length - Loc.PadSuffix.Length);
                Assert.True(Loc.Has(Loc.English, baseKey), key + " has no keyboard version");
            }
        }

        [Fact]
        public void ControllerVersionsKeepThePlaceholders()
        {
            var placeholder = new Regex(@"\{\d+\}");
            foreach (string key in Loc.AllKeys.Where(k => k.EndsWith(Loc.PadSuffix)))
            {
                string baseKey = key.Substring(0, key.Length - Loc.PadSuffix.Length);
                foreach (string lang in Loc.SupportedLanguages)
                {
                    Loc.SetLanguage(lang);
                    var a = placeholder.Matches(Loc.Get(baseKey)).Cast<Match>().Select(m => m.Value).OrderBy(x => x).ToArray();
                    Loc.PadMode = true;
                    var b = placeholder.Matches(Loc.Get(baseKey)).Cast<Match>().Select(m => m.Value).OrderBy(x => x).ToArray();
                    Loc.PadMode = false;
                    Assert.True(a.SequenceEqual(b), lang + " " + key);
                }
            }
            Loc.SetLanguage("en");
        }

        [Fact]
        public void ControllerVersionsNameKeyboardKeysOnlyAsKeyboardAlternatives()
        {
            // An English controller text that mentions a keyboard key must say it needs the keyboard.
            var keyboardKey = new Regex(@"\b(Enter|Escape|Tab|Page Up|Page Down|Home|Ctrl\+|Shift\+|F1|F5)");
            foreach (string key in Loc.AllKeys.Where(k => k.EndsWith(Loc.PadSuffix)))
            {
                string text = Loc.EnglishText(key);
                if (keyboardKey.IsMatch(text)) Assert.True(text.Contains("keyboard"), key + ": " + text);
            }
        }
    }
}
