using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Core;
using UnityEngine;

namespace HometownStoreAccess.Game
{
    /// <summary>Names and descriptions taken from the game's own localization tables, plus summaries built from game data.</summary>
    internal static class GameText
    {
        public static string Name(Item item) => item == null ? "?" : Safe(() => item.detailsKey.GetName(item.name, LanguageControl.CurrentLanguage), item.name);
        public static string Description(Item item) => item == null ? string.Empty : Safe(() => item.detailsKey.GetDescription(item.name, LanguageControl.CurrentLanguage), string.Empty);
        public static string Name(Building b) => b == null ? "?" : Safe(() => b.detailsKey.GetName(b.name, LanguageControl.CurrentLanguage), b.name);
        public static string Description(Building b) => b == null ? string.Empty : Safe(() => b.detailsKey.GetDescription(b.name, LanguageControl.CurrentLanguage), string.Empty);
        public static string Name(Customer c) => c == null ? "?" : Safe(() => c.detailsKey.GetName(c.name, LanguageControl.CurrentLanguage), c.name);
        public static string Description(Customer c) => c == null ? string.Empty : Safe(() => c.detailsKey.GetDescription(c.name, LanguageControl.CurrentLanguage), string.Empty);
        public static string Name(LocationTrade l) => l == null ? "?" : Safe(() => l.detailsKey.GetName(l.name), l.name);
        public static string Description(LocationTrade l) => l == null ? string.Empty : Safe(() => l.detailsKey.GetDescription(l.name), string.Empty);
        public static string Name(CraftingRecipe r) => r == null ? Loc.Get("recipe_none") : Safe(() => r.detailsKey.GetName(r.name, LanguageControl.CurrentLanguage), r.name);
        public static string Name(Journal j) => j == null ? "?" : Safe(() => j.detailsKey.GetName(j.name, LanguageControl.CurrentLanguage), j.name);
        public static string VehicleName(Vehicle v) => Safe(() => LocalizationManager.GetString(LocalizationTable.Vehicle_Name, v.ToString()), v.ToString());

        public static string Money(int amount) => Loc.Get("money_baht", amount);
        public static string Money(float amount) => Loc.Get("money_baht", Mathf.RoundToInt(amount));

        public static string Stack(ItemSlotData s) => s == null || s.ID == null ? "?" : Loc.Get("item_stack", Name(s.ID), s.currentAmount);

        /// <summary>"Candy 12, Gum 5" — combines stacks of the same item.</summary>
        public static string Contents(Inventory inv, int maxEntries = 8)
        {
            if (inv == null || inv.ItemList.Count == 0) return Loc.Get("state_empty");
            var parts = new List<string>();
            foreach (KeyValuePair<Item, int> kv in inv.SummaryItemDictionary())
            {
                parts.Add(Loc.Get("item_stack", Name(kv.Key), kv.Value));
                if (parts.Count >= maxEntries) break;
            }
            return string.Join(", ", parts.ToArray());
        }

        public static string Slots(Inventory inv)
        {
            if (inv == null) return string.Empty;
            // The delivery truck has a practically unlimited 9999 slots; only the count matters there.
            if (inv.slotLimit >= 999) return Loc.Get("stacks_count", inv.ItemList.Count);
            return Loc.Get("slots_used", inv.ItemList.Count, inv.slotLimit);
        }

        /// <summary>Short description of a placed building for cursor movement.</summary>
        public static string BuildingSummary(BuildingObject b)
        {
            if (b == null) return string.Empty;
            string name = Name(b.ID);
            switch (b)
            {
                case ShelfObject shelf:
                    return TextCleaner.Join(", ", name, shelf.ShelfInventory.ItemList.Count == 0 ? Loc.Get("state_empty") : Contents(shelf.ShelfInventory, 4));
                case StorageObject _:
                    return TextCleaner.Join(", ", name, Loc.Get("storage_short", ResourceManager.Instance.storage.ItemList.Count, ResourceManager.Instance.storage.slotLimit));
                case ManufactureObject m:
                    return TextCleaner.Join(", ", name, Name(m.currentRecipe));
                case CashierObject c:
                    int queue = c.queueCustomer != null ? c.queueCustomer.Count : 0;
                    return queue > 0 ? TextCleaner.Join(", ", name, Loc.Get("cashier_queue", queue)) : name;
                default:
                    return name;
            }
        }

        /// <summary>Full details of a placed building (I key on the shop floor).</summary>
        public static string BuildingDetails(BuildingObject b)
        {
            if (b == null) return string.Empty;
            var parts = new List<string> { Name(b.ID), Description(b.ID), Loc.Get("building_size", b.ID.sizeX) };
            if (b.ID.upkeepCost > 0) parts.Add(Loc.Get("building_upkeep", b.ID.upkeepCost));
            switch (b)
            {
                case ShelfObject shelf:
                    parts.Add(Slots(shelf.ShelfInventory));
                    parts.Add(Loc.Get("contents", Contents(shelf.ShelfInventory, 20)));
                    parts.Add(Loc.Get("can_hold", string.Join(", ", shelf.ShelfID.storeAbleItems.Select(Name).ToArray())));
                    break;
                case StorageObject _:
                    parts.Add(Loc.Get("storage_long", ResourceManager.Instance.storage.ItemList.Count, ResourceManager.Instance.storage.slotLimit));
                    parts.Add(Loc.Get("contents", Contents(ResourceManager.Instance.storage, 20)));
                    break;
                case ManufactureObject m:
                    parts.Add(RecipeSummary(m.currentRecipe));
                    if (m.currentRecipe != null) parts.Add(Loc.Get("manufacture_progress", m.todayDoneTime, m.playerLimitTime));
                    parts.Add(Loc.Get("manufacture_input", Contents(m.InputInventory)));
                    parts.Add(Loc.Get("manufacture_output", Contents(m.OutputInventory)));
                    break;
                case CashierObject c:
                    parts.Add(Loc.Get("cashier_queue", c.queueCustomer != null ? c.queueCustomer.Count : 0));
                    break;
            }
            parts.Add(Loc.Get("hint_open_building"));
            return TextCleaner.Join(". ", parts.ToArray());
        }

        public static string RecipeSummary(CraftingRecipe r)
        {
            if (r == null) return Loc.Get("recipe_none");
            string inputs = string.Join(", ", r.itemInput.Select(s => Loc.Get("item_stack", Name(s.ID), s.currentAmount)).ToArray());
            string outputs = string.Join(", ", r.itemOutput.Select(s => Loc.Get("item_stack", Name(s.ID), s.currentAmount)).ToArray());
            return Loc.Get("recipe_summary", Name(r), inputs, outputs, r.costMoneyPerTime, r.outputLimitPerDay);
        }

        public static string ItemDetails(Item item)
        {
            if (item == null) return string.Empty;
            var shelves = ResourceManager.Instance != null ? ResourceManager.Instance.AllStorableShelves(item).Select(s => Name(s)).Distinct().ToArray() : new string[0];
            return TextCleaner.Join(". ", Name(item), Description(item),
                Loc.Get("item_value", Mathf.RoundToInt(item.marketValue)),
                Loc.Get("item_stack_max", item.maxAmount),
                shelves.Length > 0 ? Loc.Get("item_shelves", string.Join(", ", shelves)) : Loc.Get("item_no_shelf"));
        }

        public static string CustomerState(CustomerObject c)
        {
            if (c == null) return string.Empty;
            switch (c.currentCustomerState)
            {
                case global::CustomerState.WalkIn: return Loc.Get("cstate_walkin");
                case global::CustomerState.Shopping: return Loc.Get("cstate_shopping");
                case global::CustomerState.TakeProduct: return Loc.Get("cstate_taking");
                case global::CustomerState.Angry: return Loc.Get("cstate_angry");
                case global::CustomerState.Checkout: return Loc.Get("cstate_checkout");
                case global::CustomerState.WalkOut: return Loc.Get("cstate_leaving");
                default: return c.currentCustomerState.ToString();
            }
        }

        public static string CustomerName(CustomerObject c) => c?.customerProfile?.customerID != null ? Name(c.customerProfile.customerID) : "?";

        public static string Phase()
        {
            GameControl gc = GameControl.Instance;
            if (gc == null) return string.Empty;
            switch (gc.CurrentGamePhase)
            {
                case GamePhase.Morning: return Safe(() => LocalizationManager.GetString(LocalizationTable.MainInterface, "Phase Morning"), "Morning");
                case GamePhase.Noon:
                    return TimeControl.Instance != null && TimeControl.Instance.ReachedRushTime()
                        ? Loc.Get("phase_closing")
                        : Safe(() => LocalizationManager.GetString(LocalizationTable.MainInterface, "Phase Daylight"), "Daylight");
                case GamePhase.Evening: return Safe(() => LocalizationManager.GetString(LocalizationTable.MainInterface, "Phase Evening"), "Evening");
                default: return Loc.Get("phase_summary");
            }
        }

        private static string Safe(System.Func<string> f, string fallback)
        {
            try
            {
                string s = TextCleaner.Clean(f());
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
