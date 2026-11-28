using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HometownStoreAccess.Core;
using HometownStoreAccess.Installer;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class LangFileTests
    {
        [Fact]
        public void ParsesEntriesSettingsAndComments()
        {
            LangFile f = LangFile.Parse("\uFEFF# comment\n@name = Español\n@base = en\nmod_loaded = Hola\\nmundo\nempty = \nbroken line\nMainInterface/Phase Morning = Mañana\n");
            Assert.Equal("Español", f.Name);
            Assert.Equal("en", f.BaseLocale);
            Assert.Equal("Hola\nmundo", f.Entries["mod_loaded"]);
            Assert.Equal("Mañana", f.Entries["MainInterface/Phase Morning"]);
            Assert.False(f.Entries.ContainsKey("empty"));
            Assert.Single(f.Errors);
        }

        [Fact]
        public void BaseLocaleDefaultsToEnglish()
        {
            Assert.Equal("en", LangFile.Parse("a = b").BaseLocale);
        }

        [Fact]
        public void TemplateRoundTripsKeysWithEmptyValues()
        {
            string t = LangFile.BuildTemplate("Header line\nSecond", new[] { new KeyValuePair<string, string>("k1", "Line\nbreak"), new KeyValuePair<string, string>("k2", "x") });
            Assert.Contains("# Line\\nbreak", t);
            Assert.Contains("k1 = ", t);
            LangFile parsed = LangFile.Parse(t);
            Assert.Empty(parsed.Entries);
            Assert.Empty(parsed.Errors);
        }
    }

    public class ExternalLanguageTests
    {
        [Fact]
        public void ExternalLanguageOverridesAndFallsBack()
        {
            try
            {
                Loc.SetExternal("es", new Dictionary<string, string> { ["mod_loaded"] = "Cargado" }, "Español");
                Assert.Contains("es", Loc.AvailableLanguages);
                Assert.Equal("es", Loc.NormalizeLanguage("es-MX"));
                Loc.SetLanguage("es");
                Assert.Equal("Cargado", Loc.Get("mod_loaded"));
                Assert.Equal("5 baht", Loc.Get("money_baht", 5));
                Assert.Equal("Español", Loc.ExternalName("es"));
                Assert.True(Loc.IsExternalOnly("es"));
            }
            finally
            {
                Loc.ClearExternal();
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void ExternalFileCanCorrectBuiltInLanguage()
        {
            try
            {
                Loc.SetExternal("th", new Dictionary<string, string> { ["state_on"] = "เปิดอยู่" });
                Loc.SetLanguage("th");
                Assert.Equal("เปิดอยู่", Loc.Get("state_on"));
                Assert.False(Loc.IsExternalOnly("th"));
            }
            finally
            {
                Loc.ClearExternal();
                Loc.SetLanguage("en");
            }
        }
    }

    public class MessageLogTests
    {
        [Fact]
        public void MessagesGoToAllAndTheirCategory()
        {
            var log = new MessageLog();
            log.Add("Kid came in.", MsgCategory.Customers);
            log.Add("Menu item", MsgCategory.All);
            Assert.Equal(2, log.Count(MsgCategory.All));
            Assert.Equal(1, log.Count(MsgCategory.Customers));
            Assert.Equal(0, log.Count(MsgCategory.Sales));
        }

        [Fact]
        public void StepsStopAtEdgesAndJump()
        {
            var log = new MessageLog();
            foreach (string s in new[] { "a", "b", "c" }) log.Add(s, MsgCategory.All);
            Assert.Equal("b", log.Step(-1, out bool edge));
            Assert.False(edge);
            Assert.Equal("a", log.Step(-1, out _));
            Assert.Equal("a", log.Step(-1, out edge));
            Assert.True(edge);
            Assert.Equal("c", log.Jump(toLast: true));
            Assert.Equal("a", log.Jump(toLast: false));
            Assert.Equal(0, log.Position);
        }

        [Fact]
        public void CategoriesCycleAndJump()
        {
            var log = new MessageLog();
            Assert.Equal(MsgCategory.Customers, log.CycleCategory(1));
            Assert.Equal(MsgCategory.All, log.CycleCategory(-1));
            Assert.Equal(MsgCategory.Notices, log.CycleCategory(-1));
            Assert.Equal(MsgCategory.All, log.JumpCategory(toLast: false));
            Assert.Equal(MsgCategory.Notices, log.JumpCategory(toLast: true));
        }

        [Fact]
        public void EmptyCategoryReturnsNull()
        {
            var log = new MessageLog();
            log.CycleCategory(1);
            Assert.Null(log.Step(-1, out _));
            Assert.Null(log.Jump(true));
        }

        [Fact]
        public void BufferIsBounded()
        {
            var log = new MessageLog(limitPerCategory: 3);
            for (int i = 0; i < 10; i++) log.Add("m" + i, MsgCategory.All);
            Assert.Equal(3, log.Count(MsgCategory.All));
            Assert.Equal("m7", log.Jump(false));
        }
    }

    public class UpdateInfoTests
    {
        private const string Json = "{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/x/y/releases/tag/v1.2.0\",\"body\":\"## What's new\\r\\n- **Scanner** for the shop\\r\\n- Fix \\\"quotes\\\" \\u00e9\",\"assets\":[{\"browser_download_url\":\"https://github.com/x/y/releases/download/v1.2.0/HometownStoreAccess-1.2.0.zip\"},{\"browser_download_url\":\"https://github.com/x/y/releases/download/v1.2.0/HometownStoreAccessInstaller.exe\"}]}";

        [Fact]
        public void ParsesRelease()
        {
            ReleaseInfo r = UpdateInfo.Parse(Json);
            Assert.Equal("v1.2.0", r.Tag);
            Assert.Equal("1.2.0", r.Version);
            Assert.EndsWith("/HometownStoreAccessInstaller.exe", r.InstallerUrl);
            Assert.Contains("Fix \"quotes\" é", r.Notes);
        }

        [Fact]
        public void NoteLinesStripMarkdown()
        {
            List<string> lines = UpdateInfo.NoteLines(UpdateInfo.Parse(Json).Notes);
            Assert.Equal("What's new", lines[0]);
            Assert.Equal("Scanner for the shop", lines[1]);
            Assert.Equal(3, lines.Count);
        }

        [Fact]
        public void NoteLinesLeaveOutTheInstallSection()
        {
            // 1.5.0: the installer shows the notes itself; how to run the installer is not news there.
            List<string> lines = UpdateInfo.NoteLines("Summary.\n\n## What's new\n\n- A\n\n## Bug fixes\n\n- B\n\n## Install\n\nClose the game, download the installer.\n");
            Assert.Equal(new[] { "Summary.", "What's new", "A", "Bug fixes", "B" }, lines.ToArray());
        }

        [Theory]
        [InlineData("v1.2.0", "1.1.9", true)]
        [InlineData("1.0.0", "1.0.0", false)]
        [InlineData("v1.0", "1.0.1", false)]
        [InlineData("2", "1.9.9", true)]
        [InlineData("v1.1.0-beta", "1.0.0", true)]
        [InlineData("garbage", "1.0.0", false)]
        [InlineData("1.0.1", "unknown", true)]
        public void ComparesVersions(string candidate, string current, bool newer)
        {
            Assert.Equal(newer, UpdateInfo.IsNewer(candidate, current));
        }

        [Fact]
        public void InvalidJsonIsNull()
        {
            Assert.Null(UpdateInfo.Parse("{\"message\":\"Not Found\"}"));
            Assert.Null(UpdateInfo.Parse(""));
        }
    }

    public class ConfigEditorTests
    {
        [Fact]
        public void CreatesFileAndSection()
        {
            using (var temp = new TempDir())
            {
                string p = Path.Combine(temp.Path, "cfg", "a.cfg");
                ConfigEditor.SetValue(p, "Speech", "GameLocale", "th");
                Assert.Contains("[Speech]", File.ReadAllText(p));
                Assert.Contains("GameLocale = th", File.ReadAllText(p));
            }
        }

        [Fact]
        public void ReplacesExistingValueInRightSection()
        {
            using (var temp = new TempDir())
            {
                string p = Path.Combine(temp.Path, "a.cfg");
                File.WriteAllText(p, "[Audio]\nGameLocale = keep\n\n[Speech]\n## comment\nGameLocale = en\nBackend = Auto\n");
                ConfigEditor.SetValue(p, "Speech", "GameLocale", "ja");
                ConfigEditor.SetValue(p, "Speech", "ModLanguage", "Auto");
                string s = File.ReadAllText(p);
                Assert.Contains("[Audio]\r\nGameLocale = keep", s);
                Assert.Contains("GameLocale = ja", s);
                Assert.DoesNotContain("GameLocale = en", s);
                Assert.Contains("ModLanguage = Auto", s);
            }
        }
    }

    public class SourceKeyCoverageTests
    {
        private static string RepoRoot()
        {
            string dir = System.AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "HometownStoreAccess.sln"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }

        [Fact]
        public void EveryKeyUsedInTheSourceExistsInEnglish()
        {
            string root = RepoRoot();
            Assert.NotNull(root);
            var keys = new HashSet<string>();
            foreach (string f in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(f), "Loc\\.Get\\(\\s*\"([a-z0-9_]+)\"")) keys.Add(m.Groups[1].Value);
            }
            // Keys built at runtime from a prefix ("cue_" + name...) are checked below.
            keys.RemoveWhere(k => k.EndsWith("_"));
            var missing = keys.Where(k => !Loc.Has(Loc.English, k)).ToList();
            Assert.True(missing.Count == 0, "Missing English strings: " + string.Join(", ", missing));
        }

        [Fact]
        public void DynamicKeysExist()
        {
            string[] dynamic =
            {
                "cue_move", "cue_wrap", "cue_edge", "cue_activate", "cue_error", "cue_placeok", "cue_placeblocked", "cue_cursortick",
                "cue_buildinghere", "cue_customerarrive", "cue_customerangry", "cue_step", "cue_arrive", "cue_beacon", "cue_open", "cue_close",
                "hist_all", "hist_customers", "hist_sales", "hist_dialog", "hist_events", "hist_shop", "hist_tutorial", "hist_notices",
                "scan_all", "scan_shelves", "scan_storageandmachines", "scan_cashiers", "scan_decorations", "scan_customers", "scan_freespots", "scan_delivery",
                "dir_n", "dir_ne", "dir_e", "dir_se", "dir_s", "dir_sw", "dir_w", "dir_nw",
            };
            var missing = dynamic.Where(k => !Loc.Has(Loc.English, k)).ToList();
            Assert.True(missing.Count == 0, "Missing: " + string.Join(", ", missing));
            foreach (MsgCategory c in MessageLog.Categories) Assert.NotEqual("hist_" + c.ToString().ToLowerInvariant(), Loc.EnglishText("hist_" + c.ToString().ToLowerInvariant()));
        }
    }
}
