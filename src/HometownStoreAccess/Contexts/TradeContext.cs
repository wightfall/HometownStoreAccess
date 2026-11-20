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
    /// <summary>
    /// A shop on the map. The products come from the shop's own data, not from the game's visible list,
    /// so every product can be reached: Ctrl+Up / Ctrl+Down (or the Category item) switch between all
    /// products, all goods, one goods category, all buildings and one building category. The game's
    /// own type tabs and filters follow, so a sighted helper sees the same list.
    /// Left/Right change the quantity in the basket (Shift: 10), Enter adds one, Ctrl+I gives details.
    /// </summary>
    internal sealed class TradeContext : ScreenContext
    {
        private const string Goods = "item";
        private const string Buildings = "shelf";

        private readonly CategoryFilter _filter;
        private LocationProfile _profile;

        public TradeContext()
        {
            _filter = new CategoryFilter(CategoryName, IsProduct) { OnChanged = SyncGameTabs };
        }

        public override string Id => "trade";
        public override int Priority => 65;

        private static TradePanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.tradePanel : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;

        public override void OnEnter()
        {
            TradePanel p = Panel;
            if (p != null && p.locationProfile != _profile)
            {
                // A different shop: start again with every product.
                _profile = p.locationProfile;
                _filter.Cycle.Reset();
            }
        }

        public override void OnExit()
        {
            // Closed (not just covered by a popup): the next visit starts with every product.
            if (!IsActive()) _profile = null;
        }

        public override string Title()
        {
            TradePanel p = Panel;
            return TextCleaner.Join(", ", UiUtil.Text(p.locationNameText), Loc.Get("trade_cash", UiUtil.Text(p.currentCashText)));
        }

        public override bool HandleInput() => _filter.HandleKeys();

        private static bool IsProduct(NavItem i) => i.Key != null && (i.Key.StartsWith("gi:") || i.Key.StartsWith("gs:"));

        private static string CategoryOf(Item item) => Goods + ":" + item.itemCategory;

        private static string CategoryOf(Building b) => Buildings + ":" + b.buildingCategory;

        private static string CategoryName(string option)
        {
            if (option == CategoryCycle.All) return Loc.Get("cat_all_products");
            if (option == Goods) return Loc.Get("cat_goods");
            if (option == Buildings) return Loc.Get("cat_buildings");
            int colon = option.IndexOf(':');
            string name = CategoryLabel(colon >= 0 ? option.Substring(colon + 1) : option);
            // Buildings are filtered by the goods they hold: "Buildings: Drinks".
            return option.StartsWith(Buildings + ":") ? Loc.Get("cat_buildings_of", name) : name;
        }

        /// <summary>The game names categories with raw enum names ("FoodAndSnack"); translate them.</summary>
        public static string CategoryLabel(string raw)
        {
            string key = "cat_" + raw;
            return Loc.Has(Loc.Language, key) || Loc.Has(Loc.English, key) ? Loc.Get(key) : TextCleaner.HumanizeName(raw);
        }

        public override void Build(List<NavItem> items)
        {
            TradePanel p = Panel;
            if (p == null || p.locationProfile == null) return;
            LocationProfile lp = p.locationProfile;
            List<TradeItemSet> goods = (lp.currentItemList ?? new List<TradeItemSet>()).Where(s => s != null && s.item != null).ToList();
            List<TradeShelfSet> buildings = (lp.currentShelfList ?? new List<TradeShelfSet>()).Where(s => s != null && s.shelf != null).ToList();
            _filter.Prepare(goods.Select(s => CategoryOf(s.item)).Concat(buildings.Select(s => CategoryOf(s.shelf))));

            string top = Loc.Get("group_trade_filters");
            if (_filter.Active) items.Add(_filter.Item(top));
            UiScan.AddControl(items, p.searchInputField, top, () => Loc.Get("label_search"));

            // The game's search box matches the start of product names; keep that.
            string search = p.searchInputField != null ? p.searchInputField.text.Trim().ToLower() : string.Empty;
            bool Found(string name) => search.Length == 0 || (name ?? string.Empty).ToLower().StartsWith(search);

            int count = 0;
            string goodsGroup = Loc.Get("group_goods");
            foreach (TradeItemSet s in goods.Where(s => _filter.Matches(CategoryOf(s.item))).OrderBy(s => GameText.Name(s.item)))
            {
                if (!Found(GameText.Name(s.item))) continue;
                items.Add(Product(p, s.item, null, goodsGroup, "gi:"));
                count++;
            }
            string buildingGroup = Loc.Get("group_buildings");
            foreach (TradeShelfSet s in buildings.Where(s => _filter.Matches(CategoryOf(s.shelf))).OrderBy(s => GameText.Name(s.shelf)))
            {
                if (!Found(GameText.Name(s.shelf))) continue;
                items.Add(Product(p, null, s.shelf, buildingGroup, "gs:"));
                count++;
            }
            if (count == 0) items.Add(new TextItem(Loc.Get("trade_nothing"), "nogoods") { Group = goodsGroup });

            // The basket: change amounts here too, without looking for the product again.
            string basket = Loc.Get("group_basket");
            foreach (Item it in p.basketItem.Keys.ToList()) items.Add(Product(p, it, null, basket, "bi:"));
            foreach (Building b in p.basketShelf.Keys.ToList()) items.Add(Product(p, null, b, basket, "bs:"));
            items.Add(new TextItem(() => Loc.Get("trade_totals", UiUtil.Text(p.summaryPriceText), UiUtil.Text(p.balanceSumText)), "totals") { Group = basket });
            UiScan.AddControl(items, p.checkOutButton, basket, () => Loc.Get("label_checkout"));
            Transform close = p.transform.Find("Close Button");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>(), basket, () => Loc.Get("label_close"));
        }

        /// <summary>Keeps the game's type tab and category filter in step with the mod's category.</summary>
        private static void SyncGameTabs(string option)
        {
            TradePanel p = Panel;
            if (p == null) return;
            try
            {
                if (option != CategoryCycle.All)
                {
                    p.currentType = option.StartsWith(Buildings) ? TradePanel.TradeType.Shelf : TradePanel.TradeType.Item;
                }
                p.selectedCategories.Clear();
                int colon = option.IndexOf(':');
                if (colon > 0 && System.Enum.IsDefined(typeof(ObjectCategory), option.Substring(colon + 1)))
                {
                    p.selectedCategories.Add((ObjectCategory)System.Enum.Parse(typeof(ObjectCategory), option.Substring(colon + 1)));
                }
                p.Refresh();
            }
            catch (System.Exception e)
            {
                Log.Debug("Trade tab sync failed: " + e.Message);
            }
        }

        private static NavItem Product(TradePanel p, Item item, Building shelf, string group, string keyPrefix)
        {
            string name = item != null ? GameText.Name(item) : GameText.Name(shelf);
            string key = keyPrefix + (item != null ? item.name : shelf.name);
            return new ActionItem(() => name, () => Change(p, item, shelf, 1), key)
            {
                Group = group,
                ValueFunc = () => ProductValue(p, item, shelf),
                DetailFunc = () => ProductDetail(p, item, shelf),
                OnActivateAlt = () => Change(p, item, shelf, 10),
                OnAdjust = (dir, large) =>
                {
                    Change(p, item, shelf, dir * (large ? 10 : 1));
                    return true;
                },
            };
        }

        private static (int stock, float price, int inBasket) Data(TradePanel p, Item item, Building shelf)
        {
            if (item != null)
            {
                TradeItemSet s = p.locationProfile.currentItemList.FirstOrDefault(x => x.item == item);
                p.basketItem.TryGetValue(item, out int n);
                return (s != null ? s.amount : 0, s != null ? s.pricePerUnit : 0f, n);
            }
            TradeShelfSet ss = p.locationProfile.currentShelfList.FirstOrDefault(x => x.shelf == shelf);
            p.basketShelf.TryGetValue(shelf, out int m);
            return (ss != null ? ss.amount : 0, ss != null ? ss.pricePerUnit : 0f, m);
        }

        private static string ProductValue(TradePanel p, Item item, Building shelf)
        {
            var (stock, price, inBasket) = Data(p, item, shelf);
            if (stock <= 0 && inBasket == 0) return Loc.Get("trade_sold_out", Mathf.CeilToInt(price));
            return Loc.Get("goods_value", Mathf.CeilToInt(price), inBasket, stock);
        }

        private static string ProductDetail(TradePanel p, Item item, Building shelf)
        {
            var (_, price, _) = Data(p, item, shelf);
            if (item != null)
            {
                int profit = Mathf.RoundToInt(item.marketValue - price);
                return TextCleaner.Join(". ", GameText.ItemDetails(item), Loc.Get("goods_profit", Mathf.RoundToInt(item.marketValue), profit), Loc.Get("hint_trade"));
            }
            return TextCleaner.Join(". ", GameText.Name(shelf), GameText.Description(shelf),
                shelf is Shelf sh ? Loc.Get("can_hold", string.Join(", ", sh.storeAbleItems.Select(GameText.Name).ToArray())) : null,
                Loc.Get("hint_trade"));
        }

        private static void Change(TradePanel p, Item item, Building shelf, int delta)
        {
            var before = Data(p, item, shelf);
            if (delta < 0) delta = -Mathf.Min(-delta, before.inBasket);
            if (delta == 0)
            {
                AudioCues.Play(Cue.Edge);
                SpeechService.Say(ProductValue(p, item, shelf), true);
                return;
            }
            if (item != null) p.ModifyBasketItemQuantity(item, delta);
            else p.ModifyBasketShelfQuantity(shelf, delta);
            var after = Data(p, item, shelf);
            if (after.inBasket == before.inBasket) AudioCues.Play(Cue.Edge);
            SpeechService.Say(TextCleaner.Join(", ", Loc.Get("in_basket", after.inBasket, after.stock), Loc.Get("trade_totals", UiUtil.Text(p.summaryPriceText), UiUtil.Text(p.balanceSumText))), true);
            // The basket section gained or lost a line: keep the focus where it is.
            AccessibilityRoot.Instance.Contexts.Refresh(false);
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_trade");
        }
    }
}
