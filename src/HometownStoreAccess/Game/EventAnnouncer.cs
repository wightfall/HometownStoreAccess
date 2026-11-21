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
        private static string _lastGuideKey;
        private static bool _rush, _ended, _storageFull;

        public static void Reset()
        {
            ShelfCounts.Clear();
            MachineDone.Clear();
            TutorialNotes.Clear();
            _deliveryCount = -1;
            _rush = _ended = _storageFull = false;
            _lastGuideKey = null;
        }

        public static void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.3f;
            GameControl gc = GameControl.Instance;
            if (gc == null || BuildingSystem.Instance == null || ResourceManager.Instance == null) return;
            PollShop(gc);
            PollTutorial();
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
                    int now = shelf.ShelfInventory.ItemList.Count;
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

            int delivery = ResourceManager.Instance.delivery.ItemList.Count;
            bool truckVisiblePhase = gc.CurrentGamePhase == GamePhase.Morning || gc.CurrentGamePhase == GamePhase.Noon;
            if (_deliveryCount >= 0 && delivery > 0 && _deliveryCount == 0 && truckVisiblePhase)
            {
                SpeechService.Say(Loc.Get("delivery_arrived", GameText.Contents(ResourceManager.Instance.delivery)), false, MsgCategory.Shop);
            }
            _deliveryCount = delivery;

            Inventory st = ResourceManager.Instance.storage;
            bool full = st.ItemList.Count >= st.slotLimit && st.slotLimit > 0 && BuildingSystem.Instance.allBuildings.Any(x => x is StorageObject);
            if (full && !_storageFull) SpeechService.Say(Loc.Get("storage_full"), false, MsgCategory.Shop);
            _storageFull = full;

            TimeControl tc = TimeControl.Instance;
            if (tc != null && open)
            {
                bool rush = tc.ReachedRushTime();
                if (rush && !_rush) SpeechService.Say(Loc.Get("closing_time"), false, MsgCategory.Events);
                _rush = rush;
                bool ended = tc.ReachedEndOfTime();
                if (ended && !_ended)
                {
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
            EventAnnouncer.Customer(Loc.Get("customer_arrived", GameText.Name(customer.customerID)), Cue.CustomerArrive, pan);
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

    [HarmonyPatch(typeof(CashierObject), nameof(CashierObject.CheckOut))]
    internal static class CheckoutPatch
    {
        private static void Prefix(CustomerObject customer, out (int value, string items, string name) __state)
        {
            __state = (0, string.Empty, string.Empty);
            if (customer == null || customer.focusToCashier) return;
            __state = (Mathf.RoundToInt(customer.inventoryBasket.CalculateAllItemsValue()), GameText.Contents(customer.inventoryBasket), GameText.CustomerName(customer));
        }

        private static void Postfix((int value, string items, string name) __state)
        {
            if (__state.value <= 0 || !Plugin.Settings.AnnounceSales.Value) return;
            SpeechService.Say(Loc.Get("sale", __state.name, __state.value, __state.items), false, MsgCategory.Sales);
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
            if (!Plugin.Settings.AnnounceCashChanges.Value || amount == 0) return;
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
        private static void Prefix(out int __state) => __state = ResourceManager.Instance.cash;

        private static void Postfix(int __state)
        {
            int spent = __state - ResourceManager.Instance.cash;
            if (spent > 0) SpeechService.Say(Loc.Get("trade_bought", spent, ResourceManager.Instance.cash), true, MsgCategory.Sales);
        }
    }
}
