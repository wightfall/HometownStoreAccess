using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Diagnostics
{
    /// <summary>
    /// Developer test hook (setting Debug.TestHooks = true). Reads one command per line from
    /// BepInEx/hsa_cmd.txt, runs them and deletes the file; results go to BepInEx/LogOutput.log.
    /// "help" writes the list of commands to the log. The README ("Test commands") describes each one.
    /// </summary>
    internal static class DebugCommands
    {
        private static System.Collections.IEnumerator PlayAllCues()
        {
            foreach (Audio.Cue cue in Enum.GetValues(typeof(Audio.Cue)))
            {
                SpeechService.Say(Audio.AudioCues.Name(cue), true);
                yield return new WaitForSecondsRealtime(1.2f);
                Audio.AudioCues.Preview(cue);
                Log.Info("[cues] played " + cue);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        private static float _nextPoll;
        private static string CommandFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "hsa_cmd.txt");

        public static Func<string> StateProvider;

        private static bool _accessWarned;

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
            catch (System.UnauthorizedAccessException e)
            {
                // A read-only command file or folder: said once in the log, then looked at less often.
                if (!_accessWarned) Log.Warn("Test commands: cannot read or delete " + file + ": " + e.Message);
                _accessWarned = true;
                _nextPoll = Time.realtimeSinceStartup + 5f;
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
                case "pick":
                {
                    // pick <dropdown name> <item from 0>: opens that dropdown and clicks the item, as the mouse does.
                    int pickAt = arg.LastIndexOf(' ');
                    int pickIndex = pickAt > 0 && int.TryParse(arg.Substring(pickAt + 1), out int pickNumber) ? pickNumber : -1;
                    string pickName = pickAt > 0 ? arg.Substring(0, pickAt) : arg;
                    TMPro.TMP_Dropdown pickDrop = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Dropdown>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(d => d.name == pickName);
                    if (pickDrop == null || pickIndex < 0 || pickIndex >= pickDrop.options.Count)
                    {
                        Log.Warn("pick: no shown dropdown " + pickName + " with item " + pickIndex);
                        break;
                    }
                    pickDrop.Show();
                    // The list's first child is its hidden template; item k follows it.
                    Transform pickList = pickDrop.transform.Find("Dropdown List/Viewport/Content");
                    Toggle pickToggle = pickList != null && pickList.childCount > pickIndex + 1 ? pickList.GetChild(pickIndex + 1).GetComponent<Toggle>() : null;
                    if (pickToggle == null)
                    {
                        Log.Warn("pick: the list of " + pickName + " has no item " + pickIndex);
                        pickDrop.Hide();
                        break;
                    }
                    // A click flips the item's toggle; the dropdown picks the item on that change.
                    pickToggle.isOn = !pickToggle.isOn;
                    Log.Info("[pick] " + pickName + " item " + pickIndex + " (" + pickDrop.options[pickIndex].text + "); value now " + pickDrop.value);
                    break;
                }
                case "say":
                    SpeechService.Say(arg);
                    break;
                case "state":
                    Log.Info("[state] " + (StateProvider != null ? StateProvider() : "(no provider)"));
                    break;
                case "perf":
                    // perf [seconds]: how long the mod's parts take per frame, written to the log.
                    Perf.Begin(arg.Length > 0 && float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float secs) ? secs : 5f);
                    break;
                case "pad":
                {
                    // pad a | pad lsdown 40 | pad rsup | pad lt: simulated controller input (frames default 2)
                    // pad connect | pad disconnect: what plugging a controller in or out does
                    string what = arg.Trim().ToLowerInvariant();
                    if (what == "connect" || what == "disconnect")
                    {
                        Controller.ConnectionChanged(what == "connect");
                        Log.Info("[cmd] controller " + what + "ed (simulated); real controller connected: " + Controller.Connected);
                        break;
                    }
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
                case "unlock":
                {
                    // unlock <place> | unlock all: makes places of the map available, as a reward does (test saves only)
                    if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        LocationTradeManager.Instance.UnlockAllLocationProfiles();
                        Log.Info("[unlock] every place");
                        break;
                    }
                    LocationTrade place = null;
                    foreach (LocationTrade t in Resources.FindObjectsOfTypeAll<LocationTrade>())
                    {
                        if (t.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) { place = t; break; }
                    }
                    if (place == null) Log.Warn("unlock: no place matches " + arg);
                    else
                    {
                        LocationTradeManager.Instance.UnlockLocationProfile(place);
                        Log.Info("[unlock] " + place.name);
                    }
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
                case "pictures":
                {
                    // pictures: the conversations' story pictures, and every portrait that changes from one line to
                    // the next at the same place (what a player sees change while reading)
                    int lineCount = 0, pictureLines = 0, changes = 0;
                    var used = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    var picEffects = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    var places = new[] { PortraitPosition.Left, PortraitPosition.Center, PortraitPosition.Right };
                    // Also the conversations no scene object holds yet: the story events bring theirs along.
                    Resources.LoadAll<RunEventType>(string.Empty);
                    Resources.LoadAll<DialogEvent>(string.Empty);
                    foreach (DialogEvent d in Resources.FindObjectsOfTypeAll<DialogEvent>())
                    {
                        if (d == null || d.dialogLines == null) continue;
                        if (arg.Length > 0)
                        {
                            // pictures <conversation>: every line of it, with its speaker and its pictures' effects.
                            if (d.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            for (int li = 0; li < d.dialogLines.Count; li++)
                            {
                                DialogLine dli = d.dialogLines[li];
                                if (dli == null) continue;
                                var shown = new List<string>();
                                foreach (PortraitPosition pp in places)
                                {
                                    PortraitData pdi = dli.GetPortraitData(pp);
                                    if (pdi != null && pdi.portrait != null) shown.Add(pp + " " + pdi.portrait.name + " " + pdi.transitionType);
                                }
                                Log.Info("[pictures] " + d.name + " " + li + " " + dli.dialogLine.GetName(d.name, LanguageControl.CurrentLanguage) + ": " + string.Join(", ", shown.ToArray()));
                            }
                            continue;
                        }
                        var before = new Dictionary<PortraitPosition, string>();
                        for (int i = 0; i < d.dialogLines.Count; i++)
                        {
                            DialogLine dl = d.dialogLines[i];
                            if (dl == null) continue;
                            lineCount++;
                            if (dl.cg_img != null)
                            {
                                pictureLines++;
                                Log.Info("[pictures] " + d.name + " line " + i + ": story picture " + dl.cg_img.name + (string.IsNullOrEmpty(dl.cg_color) ? string.Empty : " (" + dl.cg_color + ")"));
                            }
                            foreach (PortraitPosition pos in places)
                            {
                                PortraitData pd = dl.GetPortraitData(pos);
                                string now = pd != null && pd.portrait != null ? pd.portrait.name : null;
                                if (now != null) used[now] = used.TryGetValue(now, out int c) ? c + 1 : 1;
                                if (pd != null && pd.portrait != null && pd.transitionType != PortraitTransitionType.None)
                                {
                                    string picEffect = pd.transitionType.ToString();
                                    picEffects[picEffect] = picEffects.TryGetValue(picEffect, out int picCount) ? picCount + 1 : 1;
                                    if (pd.transitionType == PortraitTransitionType.Shake) Log.Info("[pictures] " + d.name + " line " + i + ": " + pos + " " + pd.portrait.name + " shakes");
                                }
                                if (before.TryGetValue(pos, out string was) && was != null && now != null && was != now)
                                {
                                    changes++;
                                    Log.Info("[pictures] " + d.name + " line " + i + ": " + pos + " " + was + " -> " + now);
                                }
                                before[pos] = now;
                            }
                        }
                    }
                    Log.Info("[pictures] " + lineCount + " lines, " + pictureLines + " with a story picture, " + changes + " portrait changes; portraits used: " + string.Join(", ", used.Select(kv => kv.Key + " x" + kv.Value).ToArray()));
                    Log.Info("[pictures] effects: " + string.Join(", ", picEffects.Select(kv => kv.Key + " x" + kv.Value).ToArray()));
                    CharacterCollectionManager picCollection = CharacterCollectionManager.Instance;
                    if (picCollection != null)
                    {
                        Log.Info("[pictures] collection: " + picCollection.allCharacterPortraits.Count + " entries: " + string.Join(", ", picCollection.allCharacterPortraits.Where(x => x != null).Select(x => x.name + "=" + (x.portrait != null ? x.portrait.name : "-")).ToArray()));
                    }
                    else Log.Info("[pictures] collection: none");
                    foreach (string picName in used.Keys)
                    {
                        CharacterPortrait picEntry = picCollection != null ? picCollection.allCharacterPortraits.FirstOrDefault(x => x != null && x.portrait != null && x.portrait.name == picName) : null;
                        Log.Info("[pictures] " + picName + ": " + (picEntry == null ? "not in the portrait collection" : "named " + picEntry.detailsKey.GetName(picEntry.name, LanguageControl.CurrentLanguage)));
                    }
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
                case "help":
                    foreach (string l in HelpText) Log.Info("[help] " + l);
                    break;
                case "places":
                    Places();
                    break;
                case "points":
                {
                    // points <n>: sets today's travel points (the map shows the new number at once)
                    if (!int.TryParse(arg, out int pts)) { Log.Warn("points: give a number"); break; }
                    LocationTradeManager.Instance.currentTravelPoint = pts;
                    SelectLocationPanel slp = UIPanelManager.Instance.GetSelectLocationPanel();
                    if (slp != null && slp.gameObject.activeInHierarchy) slp.Refresh();
                    Log.Info("[points] " + pts + " of " + LocationTradeManager.Instance.maxTravelPoint);
                    break;
                }
                case "goto":
                {
                    // goto <place>: does what arriving there does (uses a travel point when you move,
                    // checks the events, opens the shop, lottery or vehicle screen), without walking the map
                    LocationTrade place = FindAsset<LocationTrade>(arg);
                    if (place == null) { Log.Warn("goto: no place matches " + arg); break; }
                    if (LocationTradeManager.Instance.GetLocationProfile(place) == null) { Log.Warn("goto: " + place.name + " has no profile in this save"); break; }
                    LocationTradeManager.Instance.ReadLocationProfile(place);
                    Log.Info("[goto] " + place.name + ", travel points left " + LocationTradeManager.Instance.currentTravelPoint);
                    break;
                }
                case "journals":
                    Journals();
                    break;
                case "journal":
                {
                    // journal <name>: adds that event to the event journal | journal check: checks every event's tasks now
                    EventJournalManager ej = EventJournalManager.Instance;
                    if (arg.Equals("check", StringComparison.OrdinalIgnoreCase))
                    {
                        ej.CheckJournalCondition();
                        Log.Info("[journal] checked; in progress: " + string.Join(", ", ej.CurrentJournals.Select(j => j.name).ToArray()));
                        break;
                    }
                    Journal found = FindAsset<Journal>(arg);
                    if (found == null) { Log.Warn("journal: nothing matches " + arg); break; }
                    if (ej.CurrentJournals.Contains(found) || ej.DoneJournals.Contains(found)) { Log.Warn("journal: " + found.name + " is already " + (ej.CurrentJournals.Contains(found) ? "in progress" : "finished")); break; }
                    ej.AddJournal(found);
                    Log.Info("[journal] added " + found.name);
                    break;
                }
                case "stock":
                {
                    // stock <item> [amount]: puts goods straight onto the first placed shelf that can hold them
                    string[] parts = arg.Split(' ');
                    bool counted = parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _);
                    int n = counted ? int.Parse(parts[parts.Length - 1]) : 1;
                    Item it = FindAsset<Item>(counted ? string.Join(" ", parts, 0, parts.Length - 1) : arg);
                    if (it == null) { Log.Warn("stock: nothing matches " + arg); break; }
                    ShelfObject shelf = BuildingSystem.Instance.allBuildings.OfType<ShelfObject>().FirstOrDefault(sh => sh.ShelfInventory.IsItemsStoreAble(it));
                    if (shelf == null) { Log.Warn("stock: no placed shelf can hold " + it.name + " (shelves for it: " + string.Join(", ", ResourceManager.Instance.AllStorableShelves(it).Select(b => b.name).Distinct().ToArray()) + ")"); break; }
                    shelf.ShelfInventory.AddItem(it, n);
                    Log.Info("[stock] " + n + " x " + it.name + " on " + shelf.ID.name);
                    break;
                }
                case "achievements":
                {
                    // achievements: every Steam achievement of the game, its Steam name and whether it is unlocked (reads only)
                    SteamIntegration steam = SteamIntegration.instance;
                    bool connected = steam != null && steam.connectedToSteam;
                    Log.Info("[achievements] Steam " + (connected ? "connected" : "not connected"));
                    foreach (E_Achievement a in Enum.GetValues(typeof(E_Achievement)))
                    {
                        string state = connected ? (steam.IsAchievementUnlocked(a) ? "unlocked" : "locked") : "?";
                        Log.Info("[achievements] " + (int)a + " " + a + ": " + state + " - " + Patches.AchievementPatch.Describe(a));
                    }
                    break;
                }
                case "cues":
                {
                    // cues: every sound cue's clip (length, peak, volume); "cues play" also plays them one by one
                    foreach (string cueLine in Audio.AudioCues.Describe()) Log.Info("[cues] " + cueLine);
                    if (arg.Trim().Equals("play", StringComparison.OrdinalIgnoreCase)) AccessibilityRoot.Instance.StartCoroutine(PlayAllCues());
                    break;
                }
                case "achievement":
                {
                    // achievement <name or number>: says the announcement of that achievement, without unlocking it on Steam
                    E_Achievement a;
                    if (int.TryParse(arg, out int number) && Enum.IsDefined(typeof(E_Achievement), number)) a = (E_Achievement)number;
                    else if (!Enum.TryParse(arg, true, out a)) { Log.Warn("achievement: give a name or number (achievements lists them)"); break; }
                    SpeechService.Say(Patches.AchievementPatch.Describe(a), false, Core.MsgCategory.Events);
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
                    Log.Warn("Unknown debug command: " + cmd + " (help lists the commands)");
                    break;
            }
        }

        /// <summary>The asset of that type with exactly that name, else the first whose name contains the text (any case).</summary>
        private static T FindAsset<T>(string query) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(query)) return null;
            List<T> all = AllAssets<T>();
            return all.FirstOrDefault(o => o.name.Equals(query, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(o => o.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Loaded assets plus those in the game's Resources folders, each once, sorted by name.</summary>
        private static List<T> AllAssets<T>() where T : UnityEngine.Object
        {
            var set = new HashSet<T>(Resources.FindObjectsOfTypeAll<T>());
            foreach (T o in Resources.LoadAll<T>("")) set.Add(o);
            return set.OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>places: every map with its places and road signs, as the map shows them.</summary>
        private static void Places()
        {
            SelectLocationPanel panel = UIPanelManager.Instance.GetSelectLocationPanel();
            LocationTradeManager lt = LocationTradeManager.Instance;
            if (panel == null || panel.allSelectLocation.Count == 0)
            {
                Log.Warn("places: the map has not been opened yet in this session (open it in the evening first)");
                return;
            }
            foreach (KeyValuePair<Content_Map, List<Content_SelectForMap>> kv in panel.allSelectLocation)
            {
                Log.Info("[places] map " + kv.Key.name + " '" + Core.TextCleaner.Clean(kv.Key.detailsKey.GetName(kv.Key.name)) + "'" + (kv.Key == panel.currentMap ? " (current)" : ""));
                foreach (Content_SelectForMap n in kv.Value)
                {
                    string what;
                    if (n is Content_SelectLocation loc && loc.LocationID != null)
                    {
                        LocationTrade id = loc.LocationID;
                        LocationProfile prof = lt.GetLocationProfile(id);
                        what = "place " + id.name + " '" + Game.GameText.Name(id) + "'";
                        what += prof == null ? ", no profile" : ", unlocked=" + prof.unlocked + ", " + prof.currentItemList.Count + " goods, " + prof.currentShelfList.Count + " buildings";
                        if (id.lotteryPanel) what += ", lottery";
                        if (id.vehiclePanel) what += ", vehicle shop";
                        if (prof != null && lt.CurrentAtLocationProfile == prof) what += ", you are here";
                        string[] events = EventJournalManager.Instance.CurrentJournals.Where(j => j != null && j.haveLocationCondition && j.locationTradeThatTrigger == id).Select(j => j.name).ToArray();
                        if (events.Length > 0) what += ", events: " + string.Join(", ", events);
                    }
                    else if (n is Content_SelectSign sign && sign.TargetMap != null) what = "road sign to " + sign.TargetMap.name;
                    else what = "marker";
                    Log.Info("[places]   " + n.name + ": " + what + (n.gameObject.activeSelf ? "" : " (hidden on the map)"));
                }
            }
        }

        /// <summary>journals: every event of the event journal, its name, state and tasks.</summary>
        private static void Journals()
        {
            EventJournalManager ej = EventJournalManager.Instance;
            foreach (Journal j in AllAssets<Journal>())
            {
                string state = ej.CurrentJournals.Contains(j) ? "in progress" : ej.DoneJournals.Contains(j) ? "finished" : "not started";
                string title = Core.TextCleaner.Clean(j.detailsKey != null ? j.detailsKey.GetName(j.name, LanguageControl.CurrentLanguage) : null);
                var tasks = new List<string>();
                if (j.haveStoreLevelCondition) tasks.Add("shop level " + j.storeLevel);
                if (j.haveBuildingCondition) tasks.Add("buildings " + string.Join("+", j.buildingShouldHave.Where(b => b != null).Select(b => b.name).ToArray()));
                if (j.haveItemCondition) tasks.Add("on shelves " + string.Join("+", j.itemShouldHave.Where(x => x != null && x.ID != null).Select(x => x.ID.name + " x" + x.currentAmount).ToArray()));
                if (j.haveLocationCondition && j.locationTradeThatTrigger != null) tasks.Add("go to " + j.locationTradeThatTrigger.name);
                if (j.haveCustomerCondition && j.customerLevelRequiredWrapper != null && j.customerLevelRequiredWrapper.id != null) tasks.Add("trust " + j.customerLevelRequiredWrapper.id.name + " " + j.customerLevelRequiredWrapper.levelRequired);
                if (j.haveCustomerUnlockAmountCondition) tasks.Add("customers " + j.customerUnlockAmount);
                if (j.haveMoneyCondition) tasks.Add("money " + j.moneyAmount);
                if (j.haveDialogCondition && j.dialogEvent != null) tasks.Add("dialogs " + string.Join("+", j.dialogEvent.Where(d => d != null).Select(d => d.name).ToArray()));
                Log.Info("[journals] " + j.name + " '" + (string.IsNullOrEmpty(title) ? "(no name; the mod says: " + Game.GameText.Name(j) + ")" : title) + "', " + state
                    + (j.showToPlayer ? "" : ", hidden") + (j.isLoopJournal ? ", repeats" : "") + (tasks.Count > 0 ? ", tasks: " + string.Join("; ", tasks.ToArray()) : ""));
            }
        }

        /// <summary>Written to the log by "help".</summary>
        private static readonly string[] HelpText =
        {
            "help: this list",
            "state: the active screen and focus | say <text>: speak | key <KeyCode> [shift] [ctrl] [alt]: a mod key press | pad <buttons> [frames]: controller input | pad connect, pad disconnect: what plugging a controller in or out does",
            "click <path>: press a button | dump [tag]: the UI tree | world: the shop grid | perf [seconds]: the mod's cost per frame",
            "cash <n>: add money | item <name> [n]: goods into storage | stock <item> [n]: goods onto a shelf | blueprint <name> [n]: buildings | buildings: building names",
            "trades: every shop's stock today | trade <shop>: open a shop | places: every map place | points <n>: travel points | goto <place>: arrive at a place | unlock <place>|all",
            "journals: every event and its tasks | journal <name>: start an event | journal check: check the events now | events / event <name>: story events | dialog [name]: conversations",
            "reward money|item|blueprint|customer|trust|budget|wish|trade|vehicle <arg>: a reward screen | open lottery|vehicle|characters",
            "achievements: the game's Steam achievements | achievement <name or number>: say its announcement (unlocks nothing)",
            "cues: every sound cue's length, peak and volume | cues play: also plays them one by one",
            "table <table> [entry] | export <locale> | missing <locale>: game text",
        };
    }
}
