using System.IO;
using System.Text.RegularExpressions;
using HometownStoreAccess.Installer;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>
    /// 1.5.0: the installer reads the installed version from the mod's DLL, offers an update only when the
    /// latest release is newer than both the installed mod and itself, and never "updates" to an older version.
    /// </summary>
    public class InstallerVersionTests
    {
        private static string ProjectVersion()
        {
            string dir = System.AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "HometownStoreAccess.sln"))) dir = Path.GetDirectoryName(dir);
            string props = File.ReadAllText(Path.Combine(dir, "Directory.Build.props"));
            return Regex.Match(props, @"<Version>([^<]+)</Version>").Groups[1].Value;
        }

        [Theory]
        [InlineData("1.5.0", "1.5.0")]
        [InlineData("1.5.0+9427cea1b2", "1.5.0")]
        [InlineData("1.4.5.0", "1.4.5")]
        [InlineData(" 1.3.1 ", "1.3.1")]
        [InlineData("1.2.3.4", "1.2.3.4")]
        [InlineData("", null)]
        [InlineData(null, null)]
        [InlineData("0.0.0.0", null)]
        [InlineData("unknown", null)]
        public void CleansVersionTexts(string raw, string expected)
        {
            Assert.Equal(expected, ModInstaller.CleanVersion(raw));
        }

        [Fact]
        public void ReadsTheVersionOfARealDll()
        {
            // The test assembly is built with the same Directory.Build.props version as the mod.
            string dll = typeof(InstallerVersionTests).Assembly.Location;
            Assert.Equal(ProjectVersion(), ModInstaller.DllVersion(dll));
            Assert.Null(ModInstaller.DllVersion(Path.Combine(Path.GetTempPath(), "no-such-file.dll")));
        }

        [Fact]
        public void TheDllWinsOverAStaleInstallRecord()
        {
            using (var temp = new TempDir())
            {
                string game = FakeGame.Create(temp.Path);
                string plugin = Path.Combine(game, "BepInEx", "plugins", "HometownStoreAccess");
                Directory.CreateDirectory(plugin);
                Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
                File.WriteAllText(Path.Combine(game, "BepInEx", "core", "BepInEx.dll"), "bepinex");
                File.WriteAllText(Path.Combine(game, "winhttp.dll"), "proxy");
                // A mod copied in by hand over an older installer's files.
                File.Copy(typeof(InstallerVersionTests).Assembly.Location, Path.Combine(plugin, "HometownStoreAccess.dll"));
                File.WriteAllText(Path.Combine(plugin, ModInstaller.RecordFile), "version=1.3.1\nbepinex_by_installer=true\n");
                File.WriteAllText(Path.Combine(plugin, ModInstaller.VersionFile), "1.3.1");
                InstallStatus s = ModInstaller.GetStatus(game);
                Assert.Equal(InstallState.Installed, s.State);
                Assert.Equal(ProjectVersion(), s.InstalledVersion);
            }
        }

        [Fact]
        public void ComparesVersions()
        {
            Assert.Equal(0, UpdateInfo.Compare("1.5", "1.5.0"));
            Assert.Equal(1, UpdateInfo.Compare("1.5.0", "1.4.5"));
            Assert.Equal(-1, UpdateInfo.Compare("v1.3.1", "1.4.5"));
            Assert.Equal(1, UpdateInfo.Compare("1.10.0", "1.9.9"));
            Assert.Null(UpdateInfo.Compare("abc", "1.0"));
            // IsNewer agrees: "1.5" and "1.5.0" are the same, so no update to the version installed.
            Assert.False(UpdateInfo.IsNewer("1.5.0", "1.5"));
            Assert.False(UpdateInfo.ShouldOffer("1.5.0", "1.5", "1.5"));
            Assert.Equal(InstallAction.Reinstall, UpdateInfo.ActionFor(true, "1.5", "1.5.0"));
        }

        [Fact]
        public void OffersTheLatestOnlyWhenNewerThanWhatYouHave()
        {
            // The case that was wrong: an old 1.3.1 installer run on an up-to-date 1.4.5 mod.
            Assert.False(UpdateInfo.ShouldOffer("1.4.5", "1.4.5", "1.3.1"));
            // A newer release than both.
            Assert.True(UpdateInfo.ShouldOffer("1.5.0", "1.4.5", "1.3.1"));
            // This installer already carries the latest; the installed mod is older (it updates itself).
            Assert.False(UpdateInfo.ShouldOffer("1.4.5", "1.3.1", "1.4.5"));
            // Nothing installed: compared with the installer only.
            Assert.True(UpdateInfo.ShouldOffer("1.5.0", null, "1.4.5"));
            Assert.False(UpdateInfo.ShouldOffer("1.4.5", null, "1.4.5"));
            // A development build newer than the latest release.
            Assert.False(UpdateInfo.ShouldOffer("1.4.5", "1.5.0", "1.5.0"));
        }

        [Fact]
        public void TheMainButtonNeverUpdatesToAnOlderVersion()
        {
            Assert.Equal(InstallAction.Install, UpdateInfo.ActionFor(false, null, "1.5.0"));
            Assert.Equal(InstallAction.Update, UpdateInfo.ActionFor(true, "1.4.5", "1.5.0"));
            Assert.Equal(InstallAction.Reinstall, UpdateInfo.ActionFor(true, "1.5.0", "1.5.0"));
            Assert.Equal(InstallAction.InstallOlder, UpdateInfo.ActionFor(true, "1.4.5", "1.3.1"));
            // A version that cannot be read: the installer's files go on as an update.
            Assert.Equal(InstallAction.Update, UpdateInfo.ActionFor(true, null, "1.5.0"));
        }
    }
}
