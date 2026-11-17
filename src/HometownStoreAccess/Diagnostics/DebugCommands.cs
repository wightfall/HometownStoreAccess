using System;
using System.IO;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Diagnostics
{
    /// <summary>
    /// Developer test hook (Debug.TestHooks = true). Reads one command per line from
    /// BepInEx/hsa_cmd.txt and deletes the file. Commands:
    ///   dump [tag]            write the UI hierarchy
    ///   world                 write the shop grid / buildings / customers
    ///   key KeyCode [shift] [ctrl] [alt]   simulate a mod key press
    ///   click Path/To/Button  invoke a button's onClick
    ///   say text              speak text
    ///   state                 log the current accessibility context and focus
    /// </summary>
    internal static class DebugCommands
    {
        private static float _nextPoll;
        private static string CommandFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "hsa_cmd.txt");

        public static Func<string> StateProvider;

        public static void Poll()
        {
            if (Time.realtimeSinceStartup < _nextPoll) return;
            _nextPoll = Time.realtimeSinceStartup + 0.2f;
            string file = CommandFile;
            if (!File.Exists(file)) return;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
                File.Delete(file);
            }
            catch (IOException)
            {
                return;
            }
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                try
                {
                    Execute(line);
                }
                catch (Exception e)
                {
                    Log.Error("Debug command '" + line + "' failed: " + e);
                }
            }
        }

        private static void Execute(string line)
        {
            Log.Info("[cmd] " + line);
            int sp = line.IndexOf(' ');
            string cmd = (sp < 0 ? line : line.Substring(0, sp)).ToLowerInvariant();
            string arg = sp < 0 ? string.Empty : line.Substring(sp + 1).Trim();
            switch (cmd)
            {
                case "dump":
                    UiDump.Write(arg.Length > 0 ? arg : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                    break;
                case "world":
                    WorldDump.Write();
                    break;
                case "key":
                {
                    string[] parts = arg.Split(' ');
                    var key = (KeyCode)Enum.Parse(typeof(KeyCode), parts[0], true);
                    bool shift = Array.IndexOf(parts, "shift") > 0;
                    bool ctrl = Array.IndexOf(parts, "ctrl") > 0;
                    bool alt = Array.IndexOf(parts, "alt") > 0;
                    ModInput.Inject(key, shift, ctrl, alt);
                    break;
                }
                case "click":
                {
                    GameObject go = GameObject.Find(arg);
                    var button = go != null ? go.GetComponent<Button>() : null;
                    if (button == null) Log.Warn("click: no active button at " + arg);
                    else button.onClick.Invoke();
                    break;
                }
                case "say":
                    SpeechService.Say(arg);
                    break;
                case "state":
                    Log.Info("[state] " + (StateProvider != null ? StateProvider() : "(no provider)"));
                    break;
                case "open":
                    // open lottery | open vehicle: panels that are only reachable later in the game
                    if (arg == "lottery") UIPanelManager.Instance.OpenLotterySlot();
                    else if (arg == "vehicle") UIPanelManager.Instance.OpenVehiclePanel();
                    else Log.Warn("open: lottery or vehicle");
                    break;
                case "table":
                {
                    // table MainMenu Continue Game: how the selected locale resolves one table and entry
                    string[] parts = arg.Split(new[] { ' ' }, 2);
                    var db = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase;
                    var sel = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                    foreach (var loc in UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales)
                    {
                        UnityEngine.Localization.Tables.StringTable t = null;
                        string err = null;
                        try { t = db.GetTable(parts[0], loc); } catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
                        var entry = t != null && parts.Length > 1 ? t.GetEntry(parts[1]) : null;
                        Log.Info("[table] " + loc.Identifier.Code + (loc == sel ? " (selected)" : "") + ": " + (t == null ? "no table " + err : t.Count + " entries") + (parts.Length > 1 ? ", entry=" + (entry == null ? "null" : "'" + entry.Value + "'") : ""));
                    }
                    if (parts.Length > 1) Log.Info("[table] GetLocalizedString: '" + db.GetLocalizedString(parts[0], parts[1]) + "'");
                    break;
                }
                case "missing":
                {
                    // missing ja: game text entries the game itself lacks in that locale, as a translation template
                    var list = LanguageFiles.MissingGameEntries(arg);
                    string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "hsa_missing_" + arg + ".txt");
                    File.WriteAllText(path, Core.LangFile.BuildTemplate("Game text missing in " + arg, list), new System.Text.UTF8Encoding(false));
                    Log.Info("[missing] " + arg + ": " + list.Count + " entries -> " + path);
                    break;
                }
                default:
                    Log.Warn("Unknown debug command: " + cmd);
                    break;
            }
        }
    }
}
