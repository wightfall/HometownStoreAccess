using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>The game text translations shipped in translations/ (they load under the player's lang files).</summary>
    public class ShippedGameTextTests
    {
        private static readonly string[] Tables =
        {
            "MainMenu", "MainInterface", "BuildingPanel", "CustomerPanel", "ShelfPanel", "DialogPanel", "EventJournal",
            "Congratulation", "ConfirmAndNotify", "Tutorial", "Vehicle_Name", "Location_Name", "Location_Description",
            "Customer_Name", "Customer_Description", "Product_Name", "Product_Description", "Shelf_Name", "Shelf_Description",
            "Journal_Name", "Dialog_Name", "Dialog_Description", "Dialog_Response_Name", "Dialog_Response_Description",
            "MessageBox_Description",
        };

        private static string Folder()
        {
            string dir = System.AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "HometownStoreAccess.sln"))) dir = Path.GetDirectoryName(dir);
            Assert.NotNull(dir);
            return Path.Combine(dir, "translations");
        }

        public static IEnumerable<object[]> Files() => new[] { "th", "ja", "id", "zh" }.Select(c => new object[] { c });

        private static string[] EntryLines(string path) =>
            File.ReadAllLines(path).Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#") && !l.TrimStart().StartsWith("@")).ToArray();

        [Theory]
        [MemberData(nameof(Files))]
        public void ParsesCleanlyWithOneEntryPerLine(string code)
        {
            string path = Path.Combine(Folder(), code + ".game.txt");
            Assert.True(File.Exists(path), path);
            LangFile f = LangFile.Load(path);
            Assert.Empty(f.Errors);
            string[] lines = EntryLines(path);
            // Same count means no duplicate keys and no key that contains '='.
            Assert.Equal(lines.Length, f.Entries.Count);
            foreach (string l in lines) Assert.Contains(" = ", l);
        }

        [Theory]
        [MemberData(nameof(Files))]
        public void KeysBelongToTheGameTables(string code)
        {
            LangFile f = LangFile.Load(Path.Combine(Folder(), code + ".game.txt"));
            var unknown = f.Entries.Keys.Where(k => k.IndexOf('/') <= 0 || !Tables.Contains(k.Substring(0, k.IndexOf('/')))).ToList();
            Assert.True(unknown.Count == 0, "Unknown tables: " + string.Join(", ", unknown.Take(10)));
        }

        [Theory]
        [MemberData(nameof(Files))]
        public void TagsAndPlaceholdersAreBalanced(string code)
        {
            LangFile f = LangFile.Load(Path.Combine(Folder(), code + ".game.txt"));
            var bad = new List<string>();
            foreach (var kv in f.Entries)
            {
                string t = kv.Value;
                if (t.Count(c => c == '{') != t.Count(c => c == '}')) bad.Add(kv.Key);
                else if (Regex.Matches(t, "<color").Count != Regex.Matches(t, "</color>").Count) bad.Add(kv.Key);
                else if (Regex.Matches(t, "<b>").Count != Regex.Matches(t, "</b>").Count) bad.Add(kv.Key);
            }
            Assert.True(bad.Count == 0, "Unbalanced: " + string.Join(", ", bad.Take(10)));
        }

        [Theory]
        [InlineData("ja")]
        [InlineData("id")]
        [InlineData("zh")]
        public void CoversTheWholeGame(string code)
        {
            LangFile f = LangFile.Load(Path.Combine(Folder(), code + ".game.txt"));
            Assert.True(f.Entries.Count > 2800, code + " has only " + f.Entries.Count + " entries");
            Assert.Contains("MainMenu/Continue Game", f.Entries.Keys);
            Assert.Contains(f.Entries.Keys, k => k.StartsWith("Dialog_Description/"));
        }
    }

    public class LangFileMergeTests
    {
        [Fact]
        public void TheUpperFileWins()
        {
            LangFile under = LangFile.Parse("@name = Built-in\na = 1\nb = 2");
            LangFile over = LangFile.Parse("@name = Mine\nb = 20\nc = 30");
            LangFile m = LangFile.Merge(under, over);
            Assert.Equal("1", m.Entries["a"]);
            Assert.Equal("20", m.Entries["b"]);
            Assert.Equal("30", m.Entries["c"]);
            Assert.Equal("Mine", m.Name);
            Assert.Equal("1", under.Entries["a"]);
            Assert.Equal("2", under.Entries["b"]);
        }

        [Fact]
        public void EmptySettingsDoNotHideTheOnesBelow()
        {
            LangFile m = LangFile.Merge(LangFile.Parse("@base = th"), LangFile.Parse("@base = "));
            Assert.Equal("th", m.BaseLocale);
        }

        [Theory]
        [InlineData("zh-HK", "zh")]
        [InlineData("ZH_hk", "zh")]
        [InlineData("ja-JP", "ja")]
        [InlineData("th", "th")]
        [InlineData("en-GB", "en")]
        [InlineData("eo", "eo")]
        [InlineData("pt-BR", "pt-br")]
        [InlineData("idn", "idn")]
        public void MapsFileCodesToLanguages(string code, string expected)
        {
            Assert.Equal(expected, LangFile.GameTextLanguage(code));
        }
    }
}
