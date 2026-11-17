using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using HometownStoreAccess.Installer;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class SteamLocatorTests
    {
        [Fact]
        public void ParsesNewLibraryFormat()
        {
            const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t\t\"label\"\t\t\"\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"4643920\"\t\t\"123\"\n\t\t}\n\t}\n}";
            var libs = SteamLocator.ParseLibraryFolders(vdf);
            Assert.Equal(2, libs.Count);
            Assert.Equal("C:\\Program Files (x86)\\Steam", libs[0]);
            Assert.Equal("D:\\SteamLibrary", libs[1]);
        }

        [Fact]
        public void ParsesOldLibraryFormat()
        {
            const string vdf = "\"LibraryFolders\"\n{\n\t\"TimeNextStatsReport\"\t\t\"1234567\"\n\t\"1\"\t\t\"E:\\\\Games\\\\Steam\"\n}";
            var libs = SteamLocator.ParseLibraryFolders(vdf);
            Assert.Single(libs);
            Assert.Equal("E:\\Games\\Steam", libs[0]);
        }

        [Fact]
        public void ParsesInstallDir()
        {
            Assert.Equal("HometownStore", SteamLocator.ParseInstallDir("\"AppState\"\n{\n\t\"appid\"\t\t\"4643920\"\n\t\"installdir\"\t\t\"HometownStore\"\n}"));
            Assert.Null(SteamLocator.ParseInstallDir("nothing"));
        }

        [Fact]
        public void FindsGameInSecondaryLibrary()
        {
            using (var temp = new TempDir())
            {
                string steam = Path.Combine(temp.Path, "Steam");
                string lib = Path.Combine(temp.Path, "Lib");
                Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
                File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
                    "\"libraryfolders\" { \"0\" { \"path\" \"" + steam.Replace("\\", "\\\\") + "\" } \"1\" { \"path\" \"" + lib.Replace("\\", "\\\\") + "\" } }");
                string game = FakeGame.Create(Path.Combine(lib, "steamapps", "common", "HometownStore"));
                Assert.Equal(game, SteamLocator.FindGame(steam));
            }
        }

        [Fact]
        public void FindsTheAppIdOfTheInstalledFolder()
        {
            using (var temp = new TempDir())
            {
                string apps = Path.Combine(temp.Path, "steamapps");
                string game = FakeGame.Create(Path.Combine(apps, "common", "HometownStore"));
                File.WriteAllText(Path.Combine(apps, "appmanifest_480.acf"), "\"AppState\" { \"appid\" \"480\" \"installdir\" \"Spacewar\" }");
                File.WriteAllText(Path.Combine(apps, "appmanifest_4643920.acf"), "\"AppState\" { \"appid\" \"4643920\" \"installdir\" \"HometownStore\" }");
                Assert.Equal("4643920", SteamLocator.AppIdFor(game));
                Assert.Equal("4643920", SteamLocator.AppIdFor(game + "\\"));
            }
        }

        [Fact]
        public void FolderOutsideSteamHasNoAppId()
        {
            using (var temp = new TempDir())
            {
                Assert.Null(SteamLocator.AppIdFor(FakeGame.Create(Path.Combine(temp.Path, "Games", "HometownStore"))));
            }
        }

        [Fact]
        public void MissingSteamReturnsNull()
        {
            Assert.Null(SteamLocator.FindGame(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
            Assert.Null(SteamLocator.FindGame(null));
        }
    }

    public class ModInstallerTests
    {
        private static MemoryStream ReleaseZip(string version, bool includeExtra = false)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                Add(zip, "winhttp.dll", "proxy");
                Add(zip, "doorstop_config.ini", "[General]");
                Add(zip, "BepInEx/core/BepInEx.dll", "bepinex");
                Add(zip, "BepInEx/plugins/HometownStoreAccess/HometownStoreAccess.dll", "mod " + version);
                Add(zip, "BepInEx/plugins/HometownStoreAccess/prism.dll", "prism");
                Add(zip, "BepInEx/plugins/HometownStoreAccess/version.txt", version);
                if (includeExtra) Add(zip, "BepInEx/plugins/HometownStoreAccess/old-file.txt", "legacy");
            }
            ms.Position = 0;
            return ms;
        }

        private static void Add(ZipArchive zip, string path, string content)
        {
            ZipArchiveEntry e = zip.CreateEntry(path);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(content);
        }

        [Fact]
        public void InstallsBepInExAndModIntoCleanGame()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                Assert.Equal(InstallState.NotInstalled, ModInstaller.GetStatus(game).State);
                InstallResult r = ModInstaller.Install(game, ReleaseZip("1.0.0"));
                Assert.True(r.Success);
                Assert.True(r.InstalledBepInEx);
                Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
                InstallStatus s = ModInstaller.GetStatus(game);
                Assert.Equal(InstallState.Installed, s.State);
                Assert.Equal("1.0.0", s.InstalledVersion);
                Assert.True(s.BepInExInstalledByMod);
            }
        }

        [Fact]
        public void UpdateKeepsExistingBepInExAndRemovesStaleModFiles()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                ModInstaller.Install(game, ReleaseZip("1.0.0", includeExtra: true));
                File.WriteAllText(Path.Combine(game, "BepInEx", "core", "BepInEx.dll"), "user's own bepinex");
                InstallResult r = ModInstaller.Install(game, ReleaseZip("1.1.0"));
                Assert.True(r.Success);
                Assert.False(r.InstalledBepInEx);
                Assert.Equal("user's own bepinex", File.ReadAllText(Path.Combine(game, "BepInEx", "core", "BepInEx.dll")));
                Assert.False(File.Exists(Path.Combine(game, "BepInEx", "plugins", "HometownStoreAccess", "old-file.txt")));
                Assert.Equal("1.1.0", ModInstaller.GetStatus(game).InstalledVersion);
                Assert.True(ModInstaller.GetStatus(game).BepInExInstalledByMod);
            }
        }

        [Fact]
        public void PreexistingBepInExIsNotMarkedAsOurs()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
                File.WriteAllText(Path.Combine(game, "BepInEx", "core", "BepInEx.dll"), "theirs");
                File.WriteAllText(Path.Combine(game, "winhttp.dll"), "theirs");
                InstallResult r = ModInstaller.Install(game, ReleaseZip("1.0.0"));
                Assert.False(r.InstalledBepInEx);
                Assert.Equal("theirs", File.ReadAllText(Path.Combine(game, "winhttp.dll")));
                Assert.False(ModInstaller.GetStatus(game).BepInExInstalledByMod);
            }
        }

        private static string LangDir(string game) => Path.Combine(game, "BepInEx", "plugins", "HometownStoreAccess", "lang");

        private static void WriteTranslations(string game)
        {
            Directory.CreateDirectory(LangDir(game));
            File.WriteAllText(Path.Combine(LangDir(game), "es.mod.txt"), "mod_loaded = Hola");
            File.WriteAllText(Path.Combine(LangDir(game), "template.mod.txt"), "# template");
            File.WriteAllText(Path.Combine(LangDir(game), "README.txt"), "shipped with the mod");
        }

        [Fact]
        public void UpdateKeepsTranslationFiles()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                ModInstaller.Install(game, ReleaseZip("1.0.0"));
                WriteTranslations(game);
                InstallResult r = ModInstaller.Install(game, ReleaseZip("1.1.0"));
                Assert.True(r.Success);
                Assert.Equal("mod_loaded = Hola", File.ReadAllText(Path.Combine(LangDir(game), "es.mod.txt")));
                Assert.True(File.Exists(Path.Combine(LangDir(game), "template.mod.txt")));
            }
        }

        [Fact]
        public void UninstallKeepsOnlyTheirOwnTranslations()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                ModInstaller.Install(game, ReleaseZip("1.0.0"));
                WriteTranslations(game);
                InstallResult r = ModInstaller.Uninstall(game, removeBepInEx: false);
                Assert.True(r.Success);
                Assert.Contains("translations_kept", r.Messages);
                Assert.True(File.Exists(Path.Combine(LangDir(game), "es.mod.txt")));
                Assert.False(File.Exists(Path.Combine(LangDir(game), "template.mod.txt")));
                Assert.False(File.Exists(Path.Combine(LangDir(game), "README.txt")));
                Assert.False(File.Exists(Path.Combine(game, "BepInEx", "plugins", "HometownStoreAccess", "HometownStoreAccess.dll")));
                Assert.Equal(InstallState.BepInExOnly, ModInstaller.GetStatus(game).State);
                Assert.Empty(ModInstaller.OtherPlugins(game));
            }
        }

        [Fact]
        public void RemovingBepInExAlsoRemovesTranslations()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                ModInstaller.Install(game, ReleaseZip("1.0.0"));
                WriteTranslations(game);
                InstallResult r = ModInstaller.Uninstall(game, removeBepInEx: true);
                Assert.True(r.Success);
                Assert.DoesNotContain("translations_kept", r.Messages);
                Assert.False(Directory.Exists(Path.Combine(game, "BepInEx")));
            }
        }

        [Fact]
        public void UninstallWithoutTranslationsRemovesThePluginFolder()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                ModInstaller.Install(game, ReleaseZip("1.0.0"));
                InstallResult r = ModInstaller.Uninstall(game, removeBepInEx: false);
                Assert.True(r.Success);
                Assert.False(Directory.Exists(Path.Combine(game, "BepInEx", "plugins", "HometownStoreAccess")));
                Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
            }
        }

        [Theory]
        [InlineData(@"lang\es.mod.txt", true)]
        [InlineData("lang/pt-BR.game.txt", true)]
        [InlineData(@"lang\template.mod.txt", false)]
        [InlineData(@"lang\README.txt", false)]
        [InlineData("es.mod.txt", false)]
        [InlineData("HometownStoreAccess.dll", false)]
        public void RecognisesTheirTranslationFiles(string rel, bool expected)
        {
            Assert.Equal(expected, ModInstaller.IsUserTranslation(rel));
        }

        [Fact]
        public void UninstallKeepsBepInExWhenOtherPluginsExist()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                ModInstaller.Install(game, ReleaseZip("1.0.0"));
                Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins", "OtherMod"));
                InstallResult r = ModInstaller.Uninstall(game, removeBepInEx: true);
                Assert.True(r.Success);
                Assert.Contains("bepinex_kept_other_plugins", r.Messages);
                Assert.True(File.Exists(Path.Combine(game, "winhttp.dll")));
                Assert.False(Directory.Exists(Path.Combine(game, "BepInEx", "plugins", "HometownStoreAccess")));
            }
        }

        [Fact]
        public void UninstallCanRemoveBepInEx()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                ModInstaller.Install(game, ReleaseZip("1.0.0"));
                InstallResult r = ModInstaller.Uninstall(game, removeBepInEx: true);
                Assert.True(r.Success);
                Assert.False(File.Exists(Path.Combine(game, "winhttp.dll")));
                Assert.False(Directory.Exists(Path.Combine(game, "BepInEx")));
                Assert.True(File.Exists(Path.Combine(game, SteamLocator.GameExe)));
                Assert.Equal(InstallState.NotInstalled, ModInstaller.GetStatus(game).State);
            }
        }

        [Fact]
        public void RefusesNonGameFolder()
        {
            using (var temp = new TempDir())
            {
                InstallResult r = ModInstaller.Install(temp.Path, ReleaseZip("1.0.0"));
                Assert.False(r.Success);
                Assert.Contains("not_game_folder", r.Messages);
            }
        }

        [Fact]
        public void RejectsUnsafeArchivePaths()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                var ms = new MemoryStream();
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) Add(zip, "BepInEx/plugins/HometownStoreAccess/../../../evil.txt", "x");
                ms.Position = 0;
                Assert.Throws<InvalidDataException>(() => ModInstaller.Install(game, ms));
            }
        }

        [Theory]
        [InlineData("BepInEx/plugins/HometownStoreAccess/HometownStoreAccess.dll", true)]
        [InlineData("BepInEx\\plugins\\HometownStoreAccess\\prism.dll", true)]
        [InlineData("BepInEx/core/BepInEx.dll", false)]
        [InlineData("winhttp.dll", false)]
        public void ClassifiesEntries(string path, bool isMod)
        {
            Assert.Equal(isMod, ModInstaller.IsModEntry(path));
        }
    }

    internal static class FakeGame
    {
        public static string Create(string folder)
        {
            Directory.CreateDirectory(Path.Combine(folder, "HometownStore_Data"));
            File.WriteAllText(Path.Combine(folder, SteamLocator.GameExe), "exe");
            return folder;
        }
    }

    internal sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hsa_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
