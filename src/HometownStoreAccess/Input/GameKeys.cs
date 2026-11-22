using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Contexts;
using HometownStoreAccess.Core;
using HometownStoreAccess.Game;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using TMPro;
using UnityEngine;

namespace HometownStoreAccess.Input
{
    /// <summary>Hotkeys that only make sense inside the game scene (cash, time, pause, next phase...).</summary>
    internal static class GameKeys
    {
        public static bool Handle(AccessibilityRoot root)
        {
            if (ModInput.Alt) return false;
            GameControl gc = GameControl.Instance;
            UIPanelManager ui = UIPanelManager.Instance;
            if (gc == null || ui == null) return false;

            if (ModInput.Down(KeyCode.C))
            {
                SpeechService.Say(GameText.Money(ResourceManager.Instance.cash), true);
                return true;
            }
            if (ModInput.Down(KeyCode.T))
            {
                SpeechService.Say(StatusText.Short(), true);
                return true;
            }
            if (ModInput.Down(KeyCode.P))
            {
                TogglePause();
                return true;
            }
            if (ModInput.Down(KeyCode.N))
            {
                GameActions.ClickIfAvailable(ui.nextPhase, Loc.Get("next_phase_unavailable"));
                return true;
            }
            if (ModInput.Down(KeyCode.B))
            {
                if (CloseBuildMode()) return true;
                Transform build = FindHudButton("BuildButton");
                if (build != null) GameActions.ClickIfAvailable(build.GetComponent<UnityEngine.UI.Button>());
                else SpeechService.Say(Loc.Get("not_available_now"), true);
                return true;
            }
            if (ModInput.Down(KeyCode.K))
            {
                SpeechService.Say(CustomerInfo.Summary(), true);
                return true;
            }
            if (ModInput.Down(KeyCode.J))
            {
                SpeechService.Say(Objective(), true);
                return true;
            }
            if (ModInput.Down(KeyCode.G))
            {
                SpeechService.Say(StorageReport(), true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// B again closes build mode, as the Close button of the build panel does (not while a building is
        /// being placed: Escape cancels that first). False when build mode is not open.
        /// </summary>
        internal static bool CloseBuildMode()
        {
            UIPanelManager ui = UIPanelManager.Instance;
            BuildingPanel panel = ui != null && ui.buildingPanelGameObject != null ? ui.buildingPanelGameObject.GetComponent<BuildingPanel>() : null;
            if (panel == null || !panel.gameObject.activeInHierarchy || Placement.Active) return false;
            Transform close = panel.transform.Find("Close Panel");
            if (close == null) return false;
            GameActions.ClickIfAvailable(close.GetComponent<UnityEngine.UI.Button>());
            return true;
        }

        internal static Transform FindHudButton(string name)
        {
            UIPanelManager ui = UIPanelManager.Instance;
            if (ui?.mainInterface == null) return null;
            foreach (GameObject root in ui.mainInterface)
            {
                if (root == null) continue;
                GameObject found = SceneCache.FindChild(root, name);
                if (found != null) return found.transform;
            }
            GameObject any = SceneCache.Find(name);
            return any != null ? any.transform : null;
        }

        private static void TogglePause()
        {
            GameControl gc = GameControl.Instance;
            if (gc.CurrentGamePhase != GamePhase.Noon)
            {
                SpeechService.Say(Loc.Get("pause_only_open"), true);
                return;
            }
            PhaseMaininterface hud = UIPanelManager.Instance.phaseMaininterface;
            if (Time.timeScale == 0f) TimeControl.Instance.SetTimeScaleX1();
            else TimeControl.Instance.SetTimeScalePause();
            if (hud == null) SpeechService.Say(StatusText.SpeedText(), true);
        }

        public static string StorageReport()
        {
            ResourceManager rm = ResourceManager.Instance;
            var parts = new List<string>
            {
                Loc.Get("storage_long", rm.storage.ItemList.Count, rm.storage.slotLimit),
                GameText.Contents(rm.storage),
            };
            if (rm.delivery.ItemList.Count > 0) parts.Add(Loc.Get("delivery_has_items", GameText.Contents(rm.delivery)));
            return TextCleaner.Join(". ", parts.ToArray());
        }

        /// <summary>The current tutorial instruction if one is showing, otherwise the active journal goals.</summary>
        public static string Objective()
        {
            string tutorial = TextCleaner.Join(". ", TutorialHints.CurrentText(), TutorialGuide.CurrentHint());
            if (!string.IsNullOrEmpty(tutorial)) return tutorial;
            EventJournalManager ej = EventJournalManager.Instance;
            if (ej != null)
            {
                var parts = new List<string>();
                foreach (Journal j in ej.CurrentJournals)
                {
                    if (j == null || !j.showToPlayer) continue;
                    var tasks = ej.GetJournalDescription(j);
                    // Done tasks are struck through in the game: say "done" for them.
                    string taskText = tasks != null ? string.Join("; ", tasks.Keys.Select(k => JournalTasks.Describe(k, markDone: true)).ToArray()) : string.Empty;
                    parts.Add(TextCleaner.Join(": ", GameText.Name(j), taskText));
                }
                if (parts.Count > 0) return TextCleaner.Join(". ", parts.ToArray());
            }
            return Loc.Get("no_objective");
        }
    }

    /// <summary>Reads the tutorial's floating instruction notes.</summary>
    internal static class TutorialHints
    {
        public static IEnumerable<GameObject> Notes()
        {
            TutorialManager t = TutorialManager.Instance;
            if (t == null) yield break;
            if (t.Tutorial0MoveCameraObject != null)
            {
                foreach (GameObject g in t.Tutorial0MoveCameraObject) yield return g;
            }
            yield return t.InBuildModeFloat;
            yield return t.doubleClickToShelf;
            yield return t.TextPutCandyTutorial3final;
            yield return t.objectTextTutorial8;
            yield return t.objectTextTutorial10;
            yield return t.objectTextTutorial11;
            yield return t.objectTextTutorial12;
            yield return t.objectTextTutorial14;
        }

        public static string TextOf(GameObject note)
        {
            if (note == null || !note.activeInHierarchy) return string.Empty;
            var texts = new List<string>();
            foreach (TMP_Text t in note.GetComponentsInChildren<TMP_Text>(false))
            {
                string s = TextCleaner.Clean(t.text);
                // Struck-through lines in the build list are already done.
                bool done = (t.fontStyle & FontStyles.Strikethrough) != 0 || (t.text != null && t.text.Contains("<s>"));
                if (t.text != null && t.text.Contains("<s>"))
                {
                    s = DescribeChecklist(t.text);
                    done = false;
                }
                if (s.Length == 0) continue;
                texts.Add(done ? Loc.Get("task_done", s) : s);
            }
            return TextCleaner.Join(". ", texts.ToArray());
        }

        /// <summary>Turns "&lt;s&gt;-Cashier&lt;/s&gt;\n-Shelf" into "Cashier done, Shelf".</summary>
        public static string DescribeChecklist(string raw)
        {
            var parts = new List<string>();
            foreach (string line in raw.Split('\n'))
            {
                bool done = line.Contains("<s>");
                string clean = TextCleaner.Clean(line).TrimStart('-', ' ');
                if (clean.Length == 0) continue;
                parts.Add(done ? Loc.Get("task_done", clean) : clean);
            }
            return string.Join(", ", parts.ToArray());
        }

        public static string CurrentText()
        {
            var parts = new List<string>();
            foreach (GameObject n in Notes())
            {
                string s = TextOf(n);
                if (s.Length > 0) parts.Add(s);
            }
            return TextCleaner.Join(". ", parts.ToArray());
        }
    }
}
