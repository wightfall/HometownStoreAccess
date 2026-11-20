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
                case "pad":
                {
                    // pad a | pad lsdown 40 | pad rsup | pad lt: simulated controller input (frames default 2)
                    var st = new Core.PadState();
                    int frames = 2;
                    foreach (string tok in arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(tok, out int f)) { frames = f; continue; }
                        switch (tok.ToLowerInvariant())
                        {
                            case "a": st.Buttons |= Core.PadButtons.A; break;
                            case "b": st.Buttons |= Core.PadButtons.B; break;
                            case "x": st.Buttons |= Core.PadButtons.X; break;
                            case "y": st.Buttons |= Core.PadButtons.Y; break;
                            case "start": st.Buttons |= Core.PadButtons.Start; break;
                            case "back": st.Buttons |= Core.PadButtons.Back; break;
                            case "lb": st.Buttons |= Core.PadButtons.LeftShoulder; break;
                            case "rb": st.Buttons |= Core.PadButtons.RightShoulder; break;
                            case "ls": st.Buttons |= Core.PadButtons.LeftThumb; break;
                            case "rs": st.Buttons |= Core.PadButtons.RightThumb; break;
                            case "up": st.Buttons |= Core.PadButtons.DPadUp; break;
                            case "down": st.Buttons |= Core.PadButtons.DPadDown; break;
                            case "left": st.Buttons |= Core.PadButtons.DPadLeft; break;
                            case "right": st.Buttons |= Core.PadButtons.DPadRight; break;
                            case "lt": st.LeftTrigger = 255; break;
                            case "rt": st.RightTrigger = 255; break;
                            case "lsup": st.LeftY = 1f; break;
                            case "lsdown": st.LeftY = -1f; break;
                            case "lsleft": st.LeftX = -1f; break;
                            case "lsright": st.LeftX = 1f; break;
                            case "rsup": st.RightY = 1f; break;
                            case "rsdown": st.RightY = -1f; break;
                            case "rsleft": st.RightX = -1f; break;
                            case "rsright": st.RightX = 1f; break;
                            default: Log.Warn("pad: unknown input " + tok); break;
                        }
                    }
                    Input.Controller.Simulate(st, frames);
                    break;
                }
                case "cash":
                    // cash 500: add (or with a minus sign remove) money
                    if (int.TryParse(arg, out int amount)) ResourceManager.Instance.ModifyCash(amount);
                    break;
                case "blueprint":
                {
                    // blueprint <name or part of it> [amount]: adds buildings to the blueprint list
                    string[] parts = arg.Split(' ');
                    int n = parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int k) ? k : 1;
                    string query = n > 1 || (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _)) ? string.Join(" ", parts, 0, parts.Length - 1) : arg;
                    Building found = null;
                    foreach (Building b in Resources.FindObjectsOfTypeAll<Building>())
                    {
                        if (b.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) { found = b; break; }
                    }
                    if (found == null) Log.Warn("blueprint: nothing matches " + query);
                    else
                    {
                        BuildingSystem.Instance.AddBlueprint(found, n);
                        Log.Info("[blueprint] added " + n + " x " + found.name);
                    }
                    break;
                }
                case "item":
                {
                    // item <name or part of it> [amount]: puts goods into the storage
                    string[] parts = arg.Split(' ');
                    bool counted = parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _);
                    int n = counted ? int.Parse(parts[parts.Length - 1]) : 1;
                    string query = counted ? string.Join(" ", parts, 0, parts.Length - 1) : arg;
                    Item found = null;
                    foreach (Item it in Resources.FindObjectsOfTypeAll<Item>())
                    {
                        if (it.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) { found = it; break; }
                    }
                    if (found == null) Log.Warn("item: nothing matches " + query);
                    else
                    {
                        ResourceManager.Instance.storage.AddItem(found, n);
                        Log.Info("[item] added " + n + " x " + found.name);
                    }
                    break;
                }
                case "trades":
                    // trades: every shop with how many goods and buildings it sells today
                    foreach (LocationProfile lp in LocationTradeManager.Instance.locationProfilesList)
                    {
                        Log.Info("[trades] " + (lp.locationTradeID != null ? lp.locationTradeID.name : "?") + ": " + lp.currentItemList.Count + " goods, " + lp.currentShelfList.Count + " buildings, unlocked=" + lp.unlocked);
                    }
                    break;
                case "trade":
                {
                    // trade <shop name or part of it>: opens that shop's trade panel without travelling
                    LocationProfile shop = null;
                    foreach (LocationProfile lp in LocationTradeManager.Instance.locationProfilesList)
                    {
                        if (lp.locationTradeID != null && lp.locationTradeID.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) { shop = lp; break; }
                    }
                    if (shop == null) Log.Warn("trade: no shop matches " + arg);
                    else UIPanelManager.Instance.OpenTradePanel(shop);
                    break;
                }
                case "reward":
                {
                    // reward money 50 | item <name> [n] | blueprint <name> | customer <name> | trust <customer> | budget <customer> | trade <shop> | vehicle Car
                    string[] parts = arg.Split(new[] { ' ' }, 2);
                    string what = parts[0].ToLowerInvariant();
                    string rest = parts.Length > 1 ? parts[1] : string.Empty;
                    T Find<T>(string q) where T : UnityEngine.Object
                    {
                        foreach (T o in Resources.FindObjectsOfTypeAll<T>())
                        {
                            if (o.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return o;
                        }
                        return null;
                    }
                    CongratulationPanel panel = null;
                    switch (what)
                    {
                        case "money":
                            panel = UIPanelManager.Instance.ShowCongratulationPanel();
                            panel.Setup(int.TryParse(rest, out int money) ? money : 50);
                            break;
                        case "item":
                        {
                            Item it = Find<Item>(rest);
                            if (it != null) { panel = UIPanelManager.Instance.ShowCongratulationPanel(); panel.Setup(it, 3); }
                            break;
                        }
                        case "blueprint":
                        {
                            Building b = Find<Building>(rest);
                            if (b != null) { panel = UIPanelManager.Instance.ShowCongratulationPanel(); panel.Setup(new BuildingBlueprint(b)); }
                            break;
                        }
                        case "customer":
                        {
                            Customer c = Find<Customer>(rest);
                            if (c != null) { panel = UIPanelManager.Instance.ShowCongratulationPanel(); panel.Setup(c); }
                            break;
                        }
                        case "trust":
                        case "budget":
                        {
                            Customer c = Find<Customer>(rest);
                            if (c == null) break;
                            var edit = new CustomerEditWrapper { customerID = c, addProfileAttribute = true, addTrustExp = 10, addBaseBudgetMinMax = new Vector2(1, 2) };
                            panel = UIPanelManager.Instance.ShowCongratulationPanel();
                            if (what == "trust") panel.Setup(edit, isTrust: true);
                            else panel.Setup(edit, new Vector2(1, 2));
                            break;
                        }
                        case "wish":
                        {
                            // reward wish <customer>: the customer now wants candy and drinking water
                            Customer c = Find<Customer>(rest);
                            Item candy = Find<Item>("Candy"), water = Find<Item>("Drinking Water");
                            if (c == null || candy == null || water == null) break;
                            var list = new ItemListWrapper();
                            list.items.Add(candy);
                            list.items.Add(water);
                            panel = UIPanelManager.Instance.ShowCongratulationPanel();
                            panel.Setup(new CustomerEditWrapper { customerID = c }, list, isPrimary: true);
                            break;
                        }
                        case "trade":
                        {
                            LocationTrade t = Find<LocationTrade>(rest);
                            if (t != null) { panel = UIPanelManager.Instance.ShowCongratulationPanel(); panel.Setup(t); }
                            break;
                        }
                        case "vehicle":
                            panel = UIPanelManager.Instance.ShowCongratulationPanel();
                            panel.Setup(Enum.TryParse(rest, true, out Vehicle v) ? v : Vehicle.Motorcycle);
                            break;
                    }
                    if (panel == null) Log.Warn("reward: nothing to show for " + arg);
                    break;
                }
                case "dialog":
                {
                    // dialog <name or part of it>: shows that conversation (dialog lists the ones with replies)
                    DialogEvent found = null;
                    foreach (DialogEvent d in Resources.FindObjectsOfTypeAll<DialogEvent>())
                    {
                        if (arg.Length == 0) { if (d.responses.Count > 0) Log.Info("[dialog] " + d.name + " (" + d.responses.Count + " replies)"); continue; }
                        if (found == null && d.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) found = d;
                    }
                    if (arg.Length == 0) break;
                    if (found == null) Log.Warn("dialog: nothing matches " + arg);
                    else GameProgressionControl.Instance.RunDialogEvent(found);
                    break;
                }
                case "events":
                    // events: every story event the game's own dev tool can run
                    foreach (RunEventType e in Resources.LoadAll<RunEventType>("")) Log.Info("[events] " + e.name);
                    break;
                case "event":
                {
                    // event <name or part of it>: runs that story event now (as the game's dev tool does)
                    RunEventType found = null;
                    foreach (RunEventType e in Resources.LoadAll<RunEventType>(""))
                    {
                        if (e.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) { found = e; break; }
                    }
                    if (found == null) Log.Warn("event: nothing matches " + arg);
                    else GameProgressionControl.Instance.ForceRunEvent(found);
                    break;
                }
                case "buildings":
                    // buildings: lists every building asset name (for the blueprint command)
                    foreach (Building b in Resources.FindObjectsOfTypeAll<Building>()) Log.Info("[buildings] " + b.name + " (" + b.GetType().Name + ", " + b.buildingCategory + ")");
                    break;
                case "open":
                    // open lottery | open vehicle: panels that are only reachable later in the game
                    if (arg == "lottery") UIPanelManager.Instance.OpenLotterySlot();
                    else if (arg == "vehicle") UIPanelManager.Instance.OpenVehiclePanel();
                    else if (arg == "characters") UIPanelManager.Instance.ToggleCharacterCollectionPanel();
                    else Log.Warn("open: lottery, vehicle or characters");
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
                case "export":
                {
                    // export ja: the game's own entries in that locale, as key = text lines
                    var list = LanguageFiles.ExistingGameEntries(arg);
                    var sb = new System.Text.StringBuilder();
                    foreach (var kv in list) sb.Append(kv.Key).Append(" = ").AppendLine(Core.LangFile.Escape(kv.Value));
                    string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "hsa_export_" + arg + ".txt");
                    File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));
                    Log.Info("[export] " + arg + ": " + list.Count + " entries -> " + path);
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
