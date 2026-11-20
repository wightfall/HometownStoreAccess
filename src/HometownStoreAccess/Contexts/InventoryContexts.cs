using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Game;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    /// <summary>Moving items between two inventories, with spoken results.</summary>
    internal static class Transfer
    {
        public enum Amount { Stack, Half, One }

        public static void Move(ItemSlotData slot, Inventory from, Inventory to, string toName, Amount amount)
        {
            if (slot == null || slot.ID == null || from == null || to == null) return;
            int n;
            switch (amount)
            {
                case Amount.Half: n = slot.currentAmount / 2; break;
                case Amount.One: n = 1; break;
                default: n = slot.currentAmount; break;
            }
            if (n <= 0)
            {
                SpeechService.Say(Loc.Get("transfer_too_few"), true);
                return;
            }
            if (!to.IsItemsStoreAble(slot.ID))
            {
                AudioCues.Play(Cue.Error);
                SpeechService.Say(Loc.Get("transfer_not_storable", GameText.Name(slot.ID), toName), true);
                return;
            }
            int before = CountOf(to, slot.ID);
            var data = amount == Amount.Stack ? slot : new ItemSlotData(slot.ID, n);
            bool ok = ResourceManager.Instance.TransferUnitToFillStack(data, from, to);
            int moved = CountOf(to, slot.ID) - before;
            if (ok && moved > 0)
            {
                SpeechService.Say(Loc.Get("transfer_done", GameText.Name(slot.ID), moved, toName), true);
            }
            // On failure the game shows its own notification (e.g. slot limit reached), which is read out.
            AccessibilityRoot.Instance.Contexts.AnnounceFocusIfChanged();
        }

        public static int CountOf(Inventory inv, Item item)
        {
            int n = 0;
            foreach (ItemSlotData s in inv.ItemList)
            {
                if (s.ID == item) n += s.currentAmount;
            }
            return n;
        }

        /// <summary>One list entry per stack in an inventory (only the stacks <paramref name="include"/> accepts).</summary>
        public static void AddStacks(List<NavItem> items, Inventory inv, Inventory target, string group, string targetName, string keyPrefix, System.Func<Item, bool> include = null)
        {
            if (inv == null) return;
            if (inv.ItemList.Count == 0)
            {
                items.Add(new TextItem(Loc.Get("state_empty"), keyPrefix + ":empty") { Group = group });
                return;
            }
            var seen = new Dictionary<Item, int>();
            int shown = 0;
            foreach (ItemSlotData s in inv.ItemList.ToList())
            {
                ItemSlotData slot = s;
                if (slot.ID == null || (include != null && !include(slot.ID))) continue;
                shown++;
                seen.TryGetValue(slot.ID, out int k);
                seen[slot.ID] = k + 1;
                var item = new ActionItem(() => GameText.Stack(slot), () => Move(slot, inv, target, targetName, Amount.Stack), keyPrefix + ":" + slot.ID.name + ":" + k)
                {
                    Group = group,
                    DetailFunc = () => TextCleaner.Join(". ", GameText.ItemDetails(slot.ID), Loc.Get("hint_transfer", targetName)),
                    OnActivateAlt = () => Move(slot, inv, target, targetName, Amount.Half),
                    OnActivateThird = () => Move(slot, inv, target, targetName, Amount.One),
                };
                if (target != null && !target.IsItemsStoreAble(slot.ID)) item.ValueFunc = () => Loc.Get("transfer_cannot_go", targetName);
                items.Add(item);
            }
            if (shown == 0) items.Add(new TextItem(Loc.Get("category_empty"), keyPrefix + ":none") { Group = group });
        }

        public static IEnumerable<string> Categories(Inventory inv)
        {
            if (inv == null) yield break;
            foreach (ItemSlotData s in inv.ItemList)
            {
                if (s.ID != null) yield return s.ID.itemCategory.ToString();
            }
        }
    }

    /// <summary>
    /// A shelf, the delivery truck or the storage, opened together with the storage (or delivery) panel.
    /// Two inventories side by side; Enter moves a stack across, Shift+Enter half, Ctrl+Enter one item.
    /// </summary>
    internal sealed class ShelfTransferContext : ScreenContext
    {
        public override string Id => "shelf";
        public override int Priority => 54;

        private static ShelfPanel Target => UIPanelManager.Instance != null ? UIPanelManager.Instance._refShelfPanel : null;
        private static ShelfPanel Source => UIPanelManager.Instance != null ? UIPanelManager.Instance._refTransferPanel : null;

        public override bool IsActive()
        {
            UIPanelManager ui = UIPanelManager.Instance;
            if (ui == null || (ui._refManufacturePanel != null && ui._refManufacturePanel.gameObject.activeInHierarchy)) return false;
            ShelfPanel t = Target;
            return t != null && t.gameObject.activeInHierarchy;
        }

        private static string NameOf(ShelfPanel p)
        {
            if (p == null) return string.Empty;
            if (p._refShelfObject != null) return GameText.Name(p._refShelfObject.ID);
            return UiUtil.Text(p.shelfNameText);
        }

        public override string Title() => NameOf(Target);

        private readonly CategoryFilter _filter = new CategoryFilter(
            o => o == CategoryCycle.All ? Loc.Get("cat_all_items") : TradeContext.CategoryLabel(o),
            i => i.Key != null && (i.Key.StartsWith("t:") || i.Key.StartsWith("s:") || i.Key.StartsWith("hold:"))
                && !i.Key.EndsWith(":none") && !i.Key.EndsWith(":empty"));

        public override bool HandleInput() => _filter.HandleKeys();

        /// <summary>A closed screen starts again with every category; a popup on top keeps the choice.</summary>
        public override void OnExit()
        {
            if (!IsActive()) _filter.Cycle.Reset();
        }

        public override void Build(List<NavItem> items)
        {
            ShelfPanel t = Target;
            ShelfPanel s = Source;
            if (t == null) return;
            bool withSource = s != null && s.gameObject.activeInHierarchy;
            var categories = new List<string>(Transfer.Categories(t._refInventory));
            if (withSource) categories.AddRange(Transfer.Categories(s._refInventory));
            if (t._refShelfObject != null) categories.AddRange(t._refShelfObject.ShelfID.storeAbleItems.Where(i => i != null).Select(i => i.itemCategory.ToString()));
            _filter.Prepare(categories);
            if (_filter.Active) items.Add(_filter.Item(string.Empty));
            System.Func<Item, bool> include = i => _filter.Matches(i.itemCategory.ToString());

            string tName = NameOf(t);
            string sName = s != null ? NameOf(s) : string.Empty;
            string tGroup = TextCleaner.Join(", ", tName, GameText.Slots(t._refInventory));
            Transfer.AddStacks(items, t._refInventory, t._refInventoryToTransfer, tGroup, sName, "t", include);
            if (withSource)
            {
                string sGroup = TextCleaner.Join(", ", sName, GameText.Slots(s._refInventory));
                Transfer.AddStacks(items, s._refInventory, s._refInventoryToTransfer, sGroup, tName, "s", include);
                string actions = Loc.Get("group_actions");
                UiScan.AddControl(items, s.giveAllButton, actions, () => Loc.Get("label_give_all", sName, tName));
                UiScan.AddControl(items, s.takeAllButton, actions, () => Loc.Get("label_take_all", tName, sName));
            }
            UiScan.AddControl(items, t.closeButton, Loc.Get("group_actions"), () => Loc.Get("label_close"));
            if (t._refShelfObject != null)
            {
                string canHold = Loc.Get("group_can_hold");
                foreach (Item it in t._refShelfObject.ShelfID.storeAbleItems)
                {
                    if (it == null || !include(it)) continue;
                    Item captured = it;
                    items.Add(new TextItem(() => TextCleaner.Join(", ", GameText.Name(captured), Loc.Get("in_storage_count", Transfer.CountOf(ResourceManager.Instance.storage, captured))), "hold:" + it.name, () => GameText.ItemDetails(captured)) { Group = canHold });
                }
            }
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_shelf");
        }
    }

    /// <summary>A machine: recipe, daily limit, progress, input and output, plus the storage panel.</summary>
    internal sealed class ManufactureContext : ScreenContext
    {
        public override string Id => "manufacture";
        public override int Priority => 55;

        private static ManufacturePanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance._refManufacturePanel : null;
        private static ShelfPanel Storage => UIPanelManager.Instance != null ? UIPanelManager.Instance._refTransferPanel : null;

        public override bool IsActive()
        {
            ManufacturePanel p = Panel;
            return p != null && p.gameObject.activeInHierarchy;
        }

        public override string Title()
        {
            ManufacturePanel p = Panel;
            return p?._refManufactureObject != null ? GameText.Name(p._refManufactureObject.ID) : Loc.Get("ctx_manufacture");
        }

        public override void Build(List<NavItem> items)
        {
            ManufacturePanel p = Panel;
            if (p == null || p._refManufactureObject == null) return;
            ManufactureObject m = p._refManufactureObject;
            ShelfPanel storage = Storage;
            string storageName = storage != null ? UiUtil.Text(storage.shelfNameText) : string.Empty;
            string machine = GameText.Name(m.ID);

            if (p.allCraftRecipePanel != null && p.allCraftRecipePanel.activeInHierarchy)
            {
                string g = Loc.Get("group_choose_recipe");
                var recipes = new List<CraftingRecipe> { null };
                recipes.AddRange(m.ManufactureID.craftingRecipe);
                foreach (CraftingRecipe r in recipes)
                {
                    CraftingRecipe recipe = r;
                    items.Add(new ActionItem(() => recipe == null ? Loc.Get("recipe_none") : GameText.RecipeSummary(recipe), () =>
                    {
                        m.SelectRecipe(recipe);
                        p.CloseAllCraftRecipePanel();
                        p.Refresh();
                        SpeechService.Say(Loc.Get("recipe_selected", GameText.Name(recipe)), true);
                    }, "recipe:" + (recipe != null ? recipe.name : "none")) { Group = g, ValueFunc = () => m.currentRecipe == recipe ? Loc.Get("state_selected") : null });
                }
                return;
            }

            string recipeGroup = Loc.Get("group_recipe");
            items.Add(new TextItem(() => GameText.RecipeSummary(m.currentRecipe), "cur_recipe") { Group = recipeGroup });
            UiScan.AddControl(items, p.openAllCraftRecipeButton, recipeGroup, () => Loc.Get("label_change_recipe"));
            if (m.currentRecipe != null)
            {
                items.Add(new ActionItem(() => Loc.Get("label_daily_limit"), null, "limit")
                {
                    Group = recipeGroup,
                    ValueFunc = () => GameControl.Instance.CurrentGamePhase == GamePhase.Noon
                        ? Loc.Get("manufacture_progress", m.todayDoneTime, m.playerLimitTime)
                        : Loc.Get("limit_value", m.playerLimitTime, m.currentRecipe.outputLimitPerDay),
                    DetailFunc = () => Loc.Get("hint_limit", m.currentRecipe.costMoneyPerTime * m.playerLimitTime),
                    OnAdjust = (dir, large) =>
                    {
                        Button b = dir > 0 ? (large ? p.addPlayerLimitMax : p.addPlayerLimit) : (large ? p.removePlayerLimitMin : p.removePlayerLimit);
                        int before = m.playerLimitTime;
                        UiUtil.Click(b.gameObject);
                        if (m.playerLimitTime == before) AudioCues.Play(Cue.Edge);
                        SpeechService.Say(Loc.Get("limit_value", m.playerLimitTime, m.currentRecipe.outputLimitPerDay), true);
                        return true;
                    },
                });
                if (p.currentCraftRecipeSlider != null && p.currentCraftRecipeSlider.gameObject.activeInHierarchy)
                {
                    Slider sl = p.currentCraftRecipeSlider;
                    items.Add(new TextItem(() => Loc.Get("craft_progress", UiUtil.Percent(sl)), "progress") { Group = recipeGroup });
                }
            }

            string inGroup = TextCleaner.Join(", ", Loc.Get("group_input"), GameText.Slots(m.InputInventory));
            Transfer.AddStacks(items, m.InputInventory, p._refInventoryToTransfer, inGroup, storageName, "in");
            string outGroup = TextCleaner.Join(", ", Loc.Get("group_output"), GameText.Slots(m.OutputInventory));
            Transfer.AddStacks(items, m.OutputInventory, p._refInventoryToTransfer, outGroup, storageName, "out");

            if (storage != null && storage.gameObject.activeInHierarchy)
            {
                string sGroup = TextCleaner.Join(", ", storageName, GameText.Slots(storage._refInventory));
                Transfer.AddStacks(items, storage._refInventory, storage._refInventoryToTransfer, sGroup, machine, "st");
                string actions = Loc.Get("group_actions");
                UiScan.AddControl(items, storage.giveAllButton, actions, () => Loc.Get("label_give_all", storageName, machine));
                UiScan.AddControl(items, storage.takeAllButton, actions, () => Loc.Get("label_take_all", machine, storageName));
            }
            UiScan.AddControl(items, p.closeButton, Loc.Get("group_actions"), () => Loc.Get("label_close"));
        }

        /// <summary>When the recipe list opens, start on the recipe the machine uses now.</summary>
        public override string PreferredFocusKey()
        {
            ManufacturePanel p = Panel;
            if (p == null || p._refManufactureObject == null || p.allCraftRecipePanel == null || !p.allCraftRecipePanel.activeInHierarchy) return null;
            CraftingRecipe current = p._refManufactureObject.currentRecipe;
            return "recipe:" + (current != null ? current.name : "none");
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_manufacture");
        }
    }
}
