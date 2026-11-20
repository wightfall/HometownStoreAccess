using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class VersionTests
    {
        private static string RepoRoot()
        {
            string dir = System.AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "HometownStoreAccess.sln"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }

        [Fact]
        public void PluginVersionMatchesTheProjectVersion()
        {
            string root = RepoRoot();
            Assert.NotNull(root);
            string props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
            string plugin = File.ReadAllText(Path.Combine(root, "src", "HometownStoreAccess", "Plugin.cs"));
            string projectVersion = Regex.Match(props, @"<Version>([^<]+)</Version>").Groups[1].Value;
            string pluginVersion = Regex.Match(plugin, @"const string Version = ""([^""]+)""").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(projectVersion));
            // BepInEx shows the plugin version and the installer compares the file version: they must agree.
            Assert.Equal(projectVersion, pluginVersion);
        }
    }
}
