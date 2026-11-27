using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Contexts;
using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using TMPro;
using UnityEngine;

namespace HometownStoreAccess.Game
{
    /// <summary>
    /// Announces what happens in the game while you are doing something else: phases, customers,
    /// sales, empty shelves, deliveries, closing time, saving, tutorial instructions, new events.
    /// </summary>
    internal static class EventAnnouncer
    {
        private static float _nextPoll;
        private static readonly Dictionary<int, int> ShelfCounts = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> MachineDone = new Dictionary<int, int>();
        private static readonly Dictionary<GameObject, string> TutorialNotes = new Dictionary<GameObject, string>();
        private static int _deliveryCount = -1;
        private static bool _truckShown;
        private static GamePhase _lastPhase;
        private static string _lastGuideKey;
        private static bool _rush, _ended, _storageFull;

        // Customers often come in and leave a few at a time: those within a second are said in one message.
        private const float GroupWindow = 1.0f;
        private static readonly List<string> Arrivals = new List<string>();
        private static readonly List<string> Departures = new List<string>();
        private static float _arrivalsSince, _departuresSince;

        // A message that already says the money (a sale, a purchase, a lottery ticket) makes the separate
        // "every change of money" message for that same change only a repeat. Only that one change is
        // left unsaid: any other change in the same frame (an event's bonus at the cashier) still is.
        private static int _cashQuietFrame = -1;
        private static System.Predicate<int> _cashQuietWhich;

        /// <summary>The money change in this frame that <paramref name="which"/> picks is said by its own message.</summary>
        public static void QuietCash(System.Predicate<int> which)
        {
            _cashQuietFrame = Time.frameCount;
            _cashQuietWhich = which;
        }

        /// <summary>After the action: a change that did not come (a ticket that could not be bought) mutes nothing.</summary>
        public static void ClearCashQuiet() => _cashQuietWhich = null;

        /// <summary>True, once, for the money change that a message already says.</summary>
        public static bool TakeCashQuiet(int amount)
        {
            if (_cashQuietFrame != Time.frameCount || _cashQuietWhich == null || !_cashQuietWhich(amount)) return false;
            _cashQuietWhich = null;
            return true;
        }

        public static void Reset()
        {
            Arrivals.Clear();
            Departures.Clear();
            ShelfCounts.Clear();
            MachineDone.Clear();
            TutorialNotes.Clear();
            _deliveryCount = -1;
            _truckShown = false;
            _rush = _ended = _storageFull = false;
            _lastGuideKey = null;
            _weather = null;
        }

        public static void Update()
        {
            FlushGroups(false);
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.3f;
            GameControl gc = GameControl.Instance;
            if (gc == null || BuildingSystem.Instance == null || ResourceManager.Instance == null) return;
            PollShop(gc);
            PollTutorial();
            PollWeather();
        }

        // The weather last said (or seen when the save loaded), as its text key.
        private static string _weather;

        /// <summary>
        /// Rain and fog (game 1.7.12) are only pictures on the screen: say when they start, change or
        /// stop (the game rolls the day's weather each morning). Clear weather when a save loads is
        /// left unsaid, as there is nothing to see.
        /// </summary>
        private static void PollWeather()
        {
            WeatherManager wm = GameText.Weather();
            if (wm == null) return;
            string now = WeatherWords.Key(wm.CurrentWeather.ToString(), wm.Intensity) ?? wm.CurrentWeather.ToString();
            if (now == _weather) return;
            bool first = _weather == null;
            _weather = now;
            if (!Plugin.Settings.AnnounceWeather.Value) return;
            if (first && wm.CurrentWeather == WeatherType.Clear) return;
            SpeechService.Say(Loc.Get("weather_now", GameText.WeatherText(wm)), false, MsgCategory.Events);
        }

        private static void PollShop(GameControl gc)
        {
            ModSettings s = Plugin.Settings;
            bool open = gc.CurrentGamePhase == GamePhase.Noon;
            foreach (BuildingObject b in BuildingSystem.Instance.allBuildings)
            {
                if (b is ShelfObject shelf)
                {
                    int id = shelf.GetInstanceID();
                    // (A slot locked to a kind of goods stays when it sells out: count the filled ones.)
                    int now = shelf.ShelfInventory.FilledSlotCount();
                    if (ShelfCounts.TryGetValue(id, out int before) && open && before > 0 && now == 0 && s.AnnounceStock.Value)
                    {
                        SpeechService.Say(Loc.Get("shelf_empty", GameText.Name(shelf.ID)), false, MsgCategory.Shop);
                    }
                    ShelfCounts[id] = now;
                }
                else if (b is ManufactureObject m)
                {
                    int id = m.GetInstanceID();
                    if (MachineDone.TryGetValue(id, out int before) && m.todayDoneTime > before && s.AnnounceStock.Value)
                    {
                        SpeechService.Say(Loc.Get("machine_done", GameText.Name(m.ID), GameText.Contents(m.OutputInventory)), false, MsgCategory.Shop);
                    }
                    MachineDone[id] = m.todayDoneTime;
                }
            }

            // The truck with goods, when the game shows it: every morning, and at any other time once it
            // holds goods and the map is closed (goods bought on the map in the evening come at once). A new
            // morning says the delivery itself (the phase message), so only other arrivals are said here.
            int delivery = ResourceManager.Instance.delivery.ItemList.Count;
            DeliveryObject truck = UI.SceneCache.Get<DeliveryObject>();
            bool shown = truck != null && truck.isVisible && delivery > 0;
            bool newMorning = gc.CurrentGamePhase == GamePhase.Morning && _lastPhase != GamePhase.Morning;
            if (_deliveryCount >= 0 && shown && !_truckShown && !newMorning)
            {
                SpeechService.Say(Loc.Get("delivery_arrived", GameText.Contents(ResourceManager.Instance.delivery)), false, MsgCategory.Shop);
            }
            _truckShown = shown;
            _lastPhase = gc.CurrentGamePhase;
            _deliveryCount = delivery;

            Inventory st = ResourceManager.Instance.storage;
            bool full = st.ItemList.Count >= st.slotLimit && st.slotLimit > 0 && BuildingSystem.Instance.allBuildings.Any(x => x is StorageObject);
            if (full && !_storageFull) SpeechService.Say(Loc.Get("storage_full"), false, MsgCategory.Shop);
            _storageFull = full;

            TimeControl tc = TimeControl.Instance;
            if (tc != null && open)
            {
                bool rush = tc.ReachedRushTime();
                bool ended = GameText.ShopClosed(tc);
                // Closed already when closing time is first seen (a save loaded after closing, or nobody in
                // the shop at closing time): only "shop closed" is said.
                if (rush && !_rush && !ended)
                {
                    FlushGroups(true);
                    SpeechService.Say(Loc.Get("closing_time"), false, MsgCategory.Events);
                }
                _rush = rush;
                if (ended && !_ended)
                {
                    // The last customers' "left the shop" comes before "everyone has gone".
                    FlushGroups(true);
                    AudioCues.Play(Cue.Arrive);
                    SpeechService.Say(Loc.Get("shop_closed_now"), false, MsgCategory.Events);
                }
                _ended = ended;
            }
            else
            {
                _rush = _ended = false;
            }
        }

        private static void PollTutorial()
        {
            if (!Plugin.Settings.AnnounceTutorial.Value) return;
            // Mod key hints for the tutorial step, spoken once the step's dialog has finished.
            bool dialogOpen = GameProgressionControl.Instance != null && GameProgressionControl.Instance.GetDialogPanelActiveSelf();
            if (!dialogOpen)
            {
                string key = TutorialGuide.CurrentKey();
                if (key != _lastGuideKey)
                {
                    _lastGuideKey = key;
                    if (key != null) SpeechService.Say(Loc.Get("tutorial_hint", Loc.Get(key)), false, MsgCategory.Tutorial);
                }
            }

            foreach (GameObject note in TutorialHints.Notes())
            {
                if (note == null) continue;
                string text = note.activeInHierarchy ? TutorialHints.TextOf(note) : string.Empty;
                TutorialNotes.TryGetValue(note, out string before);
                if (text != (before ?? string.Empty))
                {
                    TutorialNotes[note] = text;
                    if (text.Length > 0) SpeechService.Say(Loc.Get("tutorial_note", text), false, MsgCategory.Tutorial);
                }
            }
        }

        public static void CustomerArrived(string name, float pan)
        {
            if (!Plugin.Settings.AnnounceCustomers.Value) return;
            if (Arrivals.Count == 0)
            {
                _arrivalsSince = Time.unscaledTime;
                AudioCues.Play(Cue.CustomerArrive, pan);
            }
            Arrivals.Add(name);
        }

        public static void CustomerLeft(string name)
        {
            if (!Plugin.Settings.AnnounceCustomersLeaving.Value) return;
            if (Departures.Count == 0) _departuresSince = Time.unscaledTime;
            Departures.Add(name);
        }

        /// <param name="now">True: say what is waiting at once (before closing time or a new phase is said).</param>
        public static void FlushGroups(bool now)
        {
            FlushGroup(Arrivals, _arrivalsSince, now, Plugin.Settings.AnnounceCustomers.Value, "customer_arrived", "customers_arrived_same", "customers_arrived_mixed");
            FlushGroup(Departures, _departuresSince, now, Plugin.Settings.AnnounceCustomersLeaving.Value, "customer_left", "customers_left_same", "customers_left_mixed");
        }

        private static void FlushGroup(List<string> names, float since, bool now, bool enabled, string one, string same, string mixed)
        {
            if (names.Count == 0 || (!now && Time.unscaledTime - since < GroupWindow)) return;
            // The setting may have been turned off while these were waiting.
            string text = enabled ? GroupedNames.Describe(names, one, same, mixed) : null;
            names.Clear();
            if (text != null) SpeechService.Say(text, false, MsgCategory.Customers);
        }

        public static void Customer(string message, Cue? cue = null, float pan = 0f)
        {
            if (!Plugin.Settings.AnnounceCustomers.Value) return;
            if (cue.HasValue) AudioCues.Play(cue.Value, pan);
            SpeechService.Say(message, false, MsgCategory.Customers);
        }

        public static float PanOfWorld(Vector3 world)
        {
            Camera cam = Camera.main;
            if (cam == null) return 0f;
            Vector3 vp = cam.WorldToViewportPoint(world);
            return Mathf.Clamp(vp.x * 2f - 1f, -1f, 1f);
        }
    }

    [HarmonyPatch(typeof(GameControl), nameof(GameControl.ChangePhase))]
    internal static class PhaseChangePatch
    {
        private static void Prefix(GameControl __instance, out GamePhase __state) => __state = __instance.CurrentGamePhase;

        private static void Postfix(GameControl __instance, GamePhase __state, GamePhase newGamePhase)
        {
            if (__instance.CurrentGamePhase == __state) return;
            EventAnnouncer.FlushGroups(true);
            switch (newGamePhase)
            {
                case GamePhase.Morning:
                    SpeechService.Say(Loc.Get("phase_morning_start", TimeControl.Instance.DayPlayerPlay), false, MsgCategory.Events);
                    if (ResourceManager.Instance.delivery.ItemList.Count > 0)
                    {
                        SpeechService.Say(Loc.Get("delivery_arrived", GameText.Contents(ResourceManager.Instance.delivery)), false, MsgCategory.Shop);
                    }
                    break;
                case GamePhase.Noon:
                    SpeechService.Say(Loc.Get("phase_noon_start"), false, MsgCategory.Events);
                    break;
                case GamePhase.Evening:
                    SpeechService.Say(Loc.Get("phase_evening_start"), false, MsgCategory.Events);
                    break;
            }
        }
    }

    [HarmonyPatch]
    internal static class TimeScalePatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TimeControl), nameof(TimeControl.SetTimeScalePause));
            yield return AccessTools.Method(typeof(TimeControl), nameof(TimeControl.SetTimeScaleX1));
            yield return AccessTools.Method(typeof(TimeControl), nameof(TimeControl.SetTimeScaleX2));
            yield return AccessTools.Method(typeof(TimeControl), nameof(TimeControl.SetTimeScaleX3));
            yield return AccessTools.Method(typeof(TimeControl), nameof(TimeControl.SetTimeScaleX4));
        }

        private static void Prefix(out float __state) => __state = Time.timeScale;

        private static void Postfix(float __state)
        {
            if (!Mathf.Approximately(__state, Time.timeScale)) SpeechService.Say(StatusText.SpeedText(), true);
        }
    }

    [HarmonyPatch(typeof(CustomerSpawnSystem), nameof(CustomerSpawnSystem.SpawnCustomer), typeof(CustomerProfile), typeof(Impact), typeof(bool))]
    internal static class CustomerSpawnPatch
    {
        private static void Postfix(CustomerProfile customer)
        {
            if (customer?.customerID == null) return;
            CustomerObject obj = CustomerSpawnSystem.Instance.customerOnScreen.LastOrDefault();
            float pan = obj != null ? EventAnnouncer.PanOfWorld(obj.transform.position) : -1f;
            EventAnnouncer.CustomerArrived(GameText.Name(customer.customerID), pan);
        }
    }

    [HarmonyPatch(typeof(CustomerObject), nameof(CustomerObject.ChangeState))]
    internal static class CustomerStatePatch
    {
        private static void Prefix(CustomerObject __instance, CustomerState newState)
        {
            if (newState != CustomerState.Angry || __instance.customerProfile?.customerID == null) return;
            var wanted = __instance.NeedItems != null && __instance.NeedItems.Count > 0
                ? string.Join(", ", __instance.NeedItems.Keys.Select(GameText.Name).ToArray())
                : string.Join(", ", __instance.customerProfile.currentPrimaryNeedItems.Where(i => i != null).Select(GameText.Name).ToArray());
            EventAnnouncer.Customer(Loc.Get("customer_angry", GameText.Name(__instance.customerProfile.customerID), wanted), Cue.CustomerAngry, EventAnnouncer.PanOfWorld(__instance.transform.position));
        }
    }

    /// <summary>A customer who walked out of the door: the game removes them when they reach the exit.</summary>
    [HarmonyPatch(typeof(CustomerObject), nameof(CustomerObject.OnDestroy))]
    internal static class CustomerLeftPatch
    {
        private static void Prefix(CustomerObject __instance)
        {
            // Not the customers removed when a save loads or the game goes back to the menu.
            if (__instance == null || !__instance.gameObject.scene.isLoaded) return;
            if (__instance.currentCustomerState != CustomerState.WalkOut || __instance.customerProfile?.customerID == null) return;
            AccessibilityRoot root = AccessibilityRoot.Instance;
            if (root == null || !root.InGame) return;
            EventAnnouncer.CustomerLeft(GameText.Name(__instance.customerProfile.customerID));
        }
    }

    [HarmonyPatch(typeof(CashierObject), nameof(CashierObject.CheckOut))]
    internal static class CheckoutPatch
    {
        private static void Prefix(CustomerObject customer, out (int value, string items, string name, bool quiet) __state)
        {
            __state = (0, string.Empty, string.Empty, false);
            if (customer == null || customer.focusToCashier) return;
            int value = Mathf.RoundToInt(customer.inventoryBasket.CalculateAllItemsValue());
            // The sale message says the new money itself. Not when the customer brings an event: its
            // reward, paid out first, might be the same amount, and both changes are then said as usual.
            bool quiet = value > 0 && Plugin.Settings.AnnounceSales.Value && (customer.carryImpact == null || !customer.carryImpact.HaveImpact());
            if (quiet) EventAnnouncer.QuietCash(a => a == value);
            __state = (value, GameText.Contents(customer.inventoryBasket), GameText.CustomerName(customer), quiet);
        }

        private static void Postfix((int value, string items, string name, bool quiet) __state)
        {
            EventAnnouncer.ClearCashQuiet();
            if (__state.value <= 0 || !Plugin.Settings.AnnounceSales.Value) return;
            string text = __state.quiet && Plugin.Settings.AnnounceCashChanges.Value
                ? Loc.Get("sale_cash", __state.name, __state.value, __state.items, ResourceManager.Instance.cash)
                : Loc.Get("sale", __state.name, __state.value, __state.items);
            SpeechService.Say(text, false, MsgCategory.Sales);
        }
    }

    [HarmonyPatch]
    internal static class MessageBoxPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MessageBoxPanel), nameof(MessageBoxPanel.SentSatisfiedMessage));
            yield return AccessTools.Method(typeof(MessageBoxPanel), nameof(MessageBoxPanel.SentNeedItemMessage));
            yield return AccessTools.Method(typeof(MessageBoxPanel), nameof(MessageBoxPanel.SentLevelUpMessage));
        }

        private static void Prefix(MessageBoxPanel __instance, out int __state) => __state = __instance.messageParent.childCount;

        private static void Postfix(MessageBoxPanel __instance, Customer complainCustomer, int __state)
        {
            if (!Plugin.Settings.AnnounceCustomerMessages.Value) return;
            if (__instance.messageParent.childCount <= __state) return;
            Transform last = __instance.messageParent.GetChild(__instance.messageParent.childCount - 1);
            var row = last.GetComponent<ContentMessage>();
            // The row shows the customer's name and their line separately; read only the line.
            string text = row != null && row.text != null
                ? TextCleaner.Clean(row.text.text)
                : string.Join(" ", last.GetComponentsInChildren<TMP_Text>(true).Select(t => TextCleaner.Clean(t.text)).Where(t => t.Length > 0).ToArray());
            if (text.Length > 0) SpeechService.Say(Loc.Get("customer_says", GameText.Name(complainCustomer), text), false, MsgCategory.Customers);
        }
    }

    [HarmonyPatch(typeof(UIPanelManager), nameof(UIPanelManager.SavingTextPopup))]
    internal static class SavingPatch
    {
        private static void Postfix() => SpeechService.Say(Loc.Get("saving"), false, MsgCategory.Events);
    }

    [HarmonyPatch(typeof(EventJournalManager), nameof(EventJournalManager.AddJournal), typeof(Journal))]
    internal static class JournalAddedPatch
    {
        private static void Prefix(EventJournalManager __instance, Journal journal, out bool __state) => __state = journal != null && !__instance.CurrentJournals.Contains(journal);

        private static void Postfix(Journal journal, bool __state)
        {
            if (__state && journal != null && journal.showToPlayer) SpeechService.Say(Loc.Get("journal_new", GameText.Name(journal)), false, MsgCategory.Events);
        }
    }

    [HarmonyPatch(typeof(StoreManager), nameof(StoreManager.StoreLevelUp))]
    internal static class StoreLevelPatch
    {
        private static void Postfix(StoreManager __instance)
        {
            FloorCursor.Reset();
            SpeechService.Say(Loc.Get("store_level_up", __instance.StoreLevel + 1), false, MsgCategory.Events);
        }
    }

    [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.ModifyCash))]
    internal static class CashPatch
    {
        private static void Postfix(int amount)
        {
            if (!Plugin.Settings.AnnounceCashChanges.Value || amount == 0 || EventAnnouncer.TakeCashQuiet(amount)) return;
            // The game gives its starting money as the scene loads, before a save sets the real amount.
            AccessibilityRoot root = AccessibilityRoot.Instance;
            // (It does so before Unity reports the scene as loaded, while the mod still thinks it is in the menu.)
            if (root != null && (!root.InGame || Time.unscaledTime - root.SceneLoadedAt < 2f)) return;
            SpeechService.Say(Loc.Get(amount > 0 ? "cash_plus" : "cash_minus", Mathf.Abs(amount), ResourceManager.Instance.cash), false, MsgCategory.Sales);
        }
    }

    [HarmonyPatch(typeof(TradePanel), nameof(TradePanel.CheckOut))]
    internal static class TradeCheckoutPatch
    {
        private static void Prefix(out int __state)
        {
            __state = ResourceManager.Instance.cash;
            // The purchase message says what is left: not the payment again.
            EventAnnouncer.QuietCash(a => a < 0);
        }

        private static void Postfix(int __state)
        {
            EventAnnouncer.ClearCashQuiet();
            int spent = __state - ResourceManager.Instance.cash;
            if (spent > 0) SpeechService.Say(Loc.Get("trade_bought", spent, ResourceManager.Instance.cash), true, MsgCategory.Sales);
        }
    }
}
