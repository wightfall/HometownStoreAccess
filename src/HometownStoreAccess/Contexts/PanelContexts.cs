using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Core;
using HometownStoreAccess.Game;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    /// <summary>End-of-day summary: customers, income and expense lines, totals, next day.</summary>
    internal sealed class SummaryContext : ScreenContext
    {
        public override string Id => "summary";
        public override int Priority => 70;
        public override bool ResetFocusOnEnter => true;

        private static SummaryPanel Panel => SummaryPanel.Instance;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;

        public override string Title() => TextCleaner.Join(", ", Loc.Get("ctx_summary"), UiUtil.Text(Panel.dayText));

        public override string Intro()
        {
            SummaryPanel p = Panel;
            return TextCleaner.Join(". ",
                Loc.Get("summary_customers", Clean(p.totalCustomerText), Clean(p.totalSatisfiedCustomerText)),
                Loc.Get("summary_income", UiUtil.Text(p.sellSumText)),
                Loc.Get("summary_expense", UiUtil.Text(p.expenseSumText)),
                Loc.Get("summary_total", UiUtil.Text(p.SumText)));
        }

        private static string Clean(TMP_Text t) => UiUtil.Text(t).TrimStart('x', '/', ' ');

        public override void Build(List<NavItem> items)
        {
            SummaryPanel p = Panel;
            if (p == null) return;
            string totals = Loc.Get("group_totals");
            items.Add(new TextItem(() => Loc.Get("summary_customers", Clean(p.totalCustomerText), Clean(p.totalSatisfiedCustomerText)), "cust") { Group = totals });
            items.Add(new TextItem(() => Loc.Get("summary_income", UiUtil.Text(p.sellSumText)), "inc") { Group = totals });
            items.Add(new TextItem(() => Loc.Get("summary_expense", UiUtil.Text(p.expenseSumText)), "exp") { Group = totals });
            items.Add(new TextItem(() => Loc.Get("summary_total", UiUtil.Text(p.SumText)), "tot") { Group = totals });
            AddBills(items, p.sellParent, Loc.Get("group_income"), "sell");
            AddBills(items, p.expenseParent, Loc.Get("group_expense"), "expense");
            string actions = Loc.Get("group_actions");
            UiScan.AddControl(items, p.nextDayButton, actions);
            UiScan.AddControl(items, p.closeButton, actions, () => Loc.Get("label_close"));
        }

        private static void AddBills(List<NavItem> items, Transform parent, string group, string key)
        {
            int n = 0;
            foreach (Transform t in parent)
            {
                var bill = t.GetComponent<Content_BillList>();
                if (bill == null || !t.gameObject.activeInHierarchy) continue;
                Content_BillList b = bill;
                items.Add(new TextItem(() => TextCleaner.Join(", ", UiUtil.Text(b.textName), UiUtil.Text(b.amount), UiUtil.Text(b.cost)), key + n++) { Group = group });
            }
            if (n == 0) items.Add(new TextItem(Loc.Get("summary_none"), key + ":none") { Group = group });
        }

        public override string ReadAll() => Intro();

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_summary");
        }
    }

    /// <summary>Customer groups: popularity, budget, trust level, wanted items, favourite.</summary>
    internal sealed class CustomerPanelContext : ScreenContext
    {
        public override string Id => "customers";
        // Above the map (60): these open from the map's buttons too.
        public override int Priority => 61;
        public override bool ResetFocusOnEnter => true;

        private static CustomerPanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.customerPanel : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;
        public override string Title() => Loc.Get("label_customers");

        public override void Build(List<NavItem> items)
        {
            CustomerPanel p = Panel;
            if (p == null) return;
            foreach (Transform t in p.contentCustomerGroupParent)
            {
                var g = t.GetComponent<ContentCustomerGroup>();
                if (g == null || g.customerProfile == null || !t.gameObject.activeInHierarchy) continue;
                ContentCustomerGroup group = g;
                CustomerProfile prof = g.customerProfile;
                items.Add(new ActionItem(() => Describe(prof), () =>
                {
                    UiUtil.Click(group.favoriteButton.gameObject);
                    SpeechService.Say(prof.IsFavorite ? Loc.Get("favorite_on") : Loc.Get("favorite_off"), true);
                }, "cust:" + prof.customerID.name)
                {
                    DetailFunc = () => TextCleaner.Join(". ", GameText.Description(prof.customerID), Loc.Get("hint_favorite")),
                    ValueFunc = () => prof.IsFavorite ? Loc.Get("favorite") : null,
                });
            }
            UiScan.AddControl(items, CloseButton(p), string.Empty, () => Loc.Get("label_close"));
        }

        private static Button CloseButton(CustomerPanel p)
        {
            Transform t = p.transform.Find("CloseButton");
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static string Describe(CustomerProfile prof)
        {
            Customer c = prof.customerID;
            int maxLevel = c.levelUpData.Count;
            string trust = prof.trustLevel >= maxLevel ? Loc.Get("trust_max") :
                Loc.Get("trust_level", prof.trustLevel, prof.currentExp, c.levelUpData[prof.trustLevel].requiredExp);
            string needs = string.Join(", ", prof.currentPrimaryNeedItems.Where(i => i != null).Select(GameText.Name).ToArray());
            string storeReq = prof.trustLevel < maxLevel && c.levelUpData[prof.trustLevel].requiredStoreLevel > StoreManager.Instance.StoreLevel
                ? TextCleaner.Clean(LocalizationManager.GetString("CustomerPanel", "StoreLevelRequired")) : null;
            return TextCleaner.Join(", ", GameText.Name(c), prof.GetPopularityStatus(),
                Loc.Get("budget_range", prof.currentBudgetMinMax.x, prof.currentBudgetMinMax.y), trust, storeReq,
                needs.Length > 0 ? Loc.Get("customer_needs", needs) : null);
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_customers");
        }
    }

    /// <summary>Event journal: current or finished events with their tasks.</summary>
    internal sealed class JournalContext : ScreenContext
    {
        public override string Id => "journal";
        // Above the map (60): these open from the map's buttons too.
        public override int Priority => 62;
        public override bool ResetFocusOnEnter => true;

        private static EventJournalPanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.GetEventJournalPanel() : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;

        public override string Title() => TextCleaner.Join(", ", Loc.Get("label_event_journal"), Panel.checkDoneJournals ? Loc.Get("journal_done") : Loc.Get("journal_in_progress"));

        public override void Build(List<NavItem> items)
        {
            EventJournalPanel p = Panel;
            if (p == null) return;
            int i = 0;
            // The panel lists the events shown to the player in the game's order: used to name an event
            // whose title is empty.
            EventJournalManager ej = EventJournalManager.Instance;
            List<Journal> shown = ej == null ? new List<Journal>()
                : (p.checkDoneJournals ? ej.DoneJournals : ej.CurrentJournals).Where(x => x != null && x.showToPlayer).ToList();
            foreach (Transform t in p.contentParent)
            {
                var j = t.GetComponent<Content_Journal>();
                if (j == null || !t.gameObject.activeInHierarchy) continue;
                string title = UiUtil.Text(j.titleEventJournalText);
                if (string.IsNullOrEmpty(title)) title = i < shown.Count ? GameText.Name(shown[i]) : Loc.Get("journal_untitled");
                var tasks = new List<string>();
                foreach (Content_TaskJournal task in j.ChildContent.GetComponentsInChildren<Content_TaskJournal>(false))
                {
                    // The game strikes through the tasks that are already done (an event in progress can
                    // have some done); in the finished events every task is done, so it is not repeated.
                    TMP_Text line = task.descriptionText;
                    string raw = line != null ? line.text : null;
                    bool done = JournalTasks.IsDone(raw) || (line != null && (line.fontStyle & FontStyles.Strikethrough) != 0);
                    string text = JournalTasks.Describe(raw, markDone: false);
                    if (text.Length == 0) continue;
                    tasks.Add(done && !p.checkDoneJournals ? Loc.Get("task_done", text) : text);
                }
                string group = title;
                items.Add(new TextItem(() => Loc.Get("task_count", tasks.Count), "j" + i) { Group = group });
                int k = 0;
                foreach (string task in tasks) items.Add(new TextItem(task, "j" + i + "t" + k++) { Group = group });
                i++;
            }
            if (i == 0 && p.ongoingEventJournal != null && p.ongoingEventJournal.activeInHierarchy)
            {
                UiScan.AddText(items, p.ongoingEventJournal.GetComponent<TMP_Text>());
            }
            string actions = Loc.Get("group_actions");
            // The game's captions for these are Thai in every language. Only one shows at a time.
            if (p.eventInprogressButton != null && p.eventInprogressButton.gameObject.activeInHierarchy)
            {
                items.Add(new ActionItem(() => Loc.Get("label_show_view", Loc.Get("journal_in_progress")), () => SwitchView(), "view") { Group = actions });
            }
            else if (p.eventDoneButton != null && p.eventDoneButton.gameObject.activeInHierarchy)
            {
                items.Add(new ActionItem(() => Loc.Get("label_show_view", Loc.Get("journal_done")), () => SwitchView(), "view") { Group = actions });
            }
            Transform close = p.transform.Find("Close");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>(), actions, () => Loc.Get("label_close"));
        }

        /// <summary>Ctrl+Up / Ctrl+Down: the journal's two views (events in progress, finished events).</summary>
        public override bool HandleInput()
        {
            if (!Input.ModInput.Ctrl || !(Input.ModInput.Down(KeyCode.UpArrow) || Input.ModInput.Down(KeyCode.DownArrow))) return false;
            return SwitchView();
        }

        /// <summary>Shows the other view, then reads its name and first event.</summary>
        private bool SwitchView()
        {
            EventJournalPanel p = Panel;
            if (p == null) return false;
            Button b = p.eventInprogressButton != null && p.eventInprogressButton.gameObject.activeInHierarchy ? p.eventInprogressButton : p.eventDoneButton;
            if (b == null || !b.gameObject.activeInHierarchy) return false;
            UiUtil.Click(b.gameObject);
            Audio.AudioCues.Play(Audio.Cue.Wrap);
            ContextManager cm = AccessibilityRoot.Instance.Contexts;
            cm.Refresh(false);
            cm.MoveToSilently(0);
            SpeechService.Say(Title(), true);
            cm.AnnounceFocus(includeGroup: true, interrupt: false);
            return true;
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_journal");
        }
    }

    /// <summary>Characters met so far.</summary>
    internal sealed class CharacterCollectionContext : ScreenContext
    {
        public override string Id => "characters";
        // Above the map (60): these open from the map's buttons too.
        public override int Priority => 67;
        public override bool ResetFocusOnEnter => true;

        private static CharacterCollectionPanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.GetCharacterCollectionPanel() : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;
        public override string Title() => TextCleaner.Join(", ", Loc.Get("ctx_characters"), UiUtil.Text(Panel.progressCountText));

        public override void Build(List<NavItem> items)
        {
            CharacterCollectionPanel p = Panel;
            if (p == null) return;
            int i = 0;
            foreach (Transform t in p.contentParent)
            {
                var c = t.GetComponent<Content_CharacterPortrait>();
                if (c == null || !t.gameObject.activeInHierarchy) continue;
                Content_CharacterPortrait portrait = c;
                int number = i + 1;
                items.Add(new TextItem(() =>
                {
                    string n = UiUtil.Text(portrait.nameText);
                    // Characters not met yet all look the same ("???"): say which one it is.
                    return n == "???" ? Loc.Get("character_numbered", number, Loc.Get("character_unknown")) : TextCleaner.Join(", ", n, UiUtil.Text(portrait.descriptionText));
                }, "char" + i++));
            }
            Transform close = p.transform.Find("Close");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>(), string.Empty, () => Loc.Get("label_close"));
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_characters");
        }
    }

    /// <summary>The storage summary pop-up (all items in storage and on shelves).</summary>
    internal sealed class StorageSummaryContext : ScreenContext
    {
        public override string Id => "storage_summary";
        // Above the map (60): these open from the map's buttons too.
        public override int Priority => 66;
        public override bool ResetFocusOnEnter => true;

        private static StorageSummaryPanel Panel
        {
            get
            {
                foreach (StorageSummaryPanel p in SceneCache.All<StorageSummaryPanel>())
                {
                    if (p != null && p.panelRoot != null && p.panelRoot.activeInHierarchy) return p;
                }
                return null;
            }
        }

        public override bool IsActive() => Panel != null;
        public override string Title() => Loc.Get("ctx_storage_summary");

        private readonly CategoryFilter _filter = new CategoryFilter(
            o => o == CategoryCycle.All ? Loc.Get("cat_all_items") : TradeContext.CategoryLabel(o),
            i => i.Key != null && i.Key.StartsWith("stock:"));

        public override bool HandleInput()
        {
            // The game does not close this list on Escape (it opens the pause menu instead).
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && CloseOpen()) return true;
            return _filter.HandleKeys();
        }

        /// <summary>Closes the open stock list; false when none is open.</summary>
        public static bool CloseOpen()
        {
            StorageSummaryPanel p = Panel;
            if (p == null) return false;
            p.Close();
            InputGuard.HoldUntilReleased(KeyCode.Escape);
            return true;
        }

        /// <summary>A closed screen starts again with every category; a popup on top keeps the choice.</summary>
        public override void OnExit()
        {
            if (!IsActive()) _filter.Cycle.Reset();
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_storage_summary");
        }

        public override void Build(List<NavItem> items)
        {
            StorageSummaryPanel p = Panel;
            if (p == null) return;
            ResourceManager rm = ResourceManager.Instance;
            Dictionary<Item, int> storage = rm.AllResourceInStorage();
            Dictionary<Item, int> shelves = rm.AllResourceOnSelf();
            var all = storage.Keys.Union(shelves.Keys).Where(i => i != null).OrderBy(GameText.Name).ToList();
            if (all.Count == 0) items.Add(new TextItem(Loc.Get("nothing_in_stock"), "none"));
            _filter.Prepare(all.Select(i => i.itemCategory.ToString()));
            if (_filter.Active) items.Add(_filter.Item(string.Empty));
            foreach (Item it in all)
            {
                if (!_filter.Matches(it.itemCategory.ToString())) continue;
                Item item = it;
                storage.TryGetValue(item, out int s);
                shelves.TryGetValue(item, out int sh);
                int total = s + sh, inStorage = s, onShelves = sh;
                items.Add(new TextItem(() => Loc.Get("stock_line", GameText.Name(item), total, inStorage, onShelves), "stock:" + item.name, () => GameText.ItemDetails(item)));
            }
            items.Add(new ActionItem(() => Loc.Get("label_close"), () => p.Close(), "close"));
        }
    }

    /// <summary>Choose the look of a cashier counter.</summary>
    internal sealed class CashierSkinContext : ScreenContext
    {
        public override string Id => "cashier_skin";
        public override int Priority => 53;

        private static SelectCashierSkinPanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.selectCashierSkinPanel : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;
        public override string Title() => Loc.Get("ctx_cashier_skin");

        private static string SkinName(Sprite s)
        {
            if (s == null) return "?";
            string n = s.name.StartsWith("Cashier_") ? s.name.Substring(8) : s.name;
            n = System.Text.RegularExpressions.Regex.Replace(n, @"_\d+$", string.Empty);
            // The game shows only pictures; the mod names a look after its file. Plain words (default,
            // cat, frozen) are translated; names of people stay as they are.
            string name = TextCleaner.HumanizeName(n);
            string key = "skin_" + name.ToLowerInvariant().Replace(' ', '_');
            return Loc.Has(Loc.Language, key) || Loc.Has(Loc.English, key) ? Loc.Get(key) : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
        }

        public override void Build(List<NavItem> items)
        {
            SelectCashierSkinPanel p = Panel;
            if (p == null) return;
            string skins = Loc.Get("group_skins");
            foreach (Transform t in p.parent)
            {
                var skin = t.GetComponent<SkinPrefab>();
                if (skin == null || !t.gameObject.activeInHierarchy) continue;
                SkinPrefab sp = skin;
                items.Add(new ButtonItem(sp.button, () => SkinName(sp.skinImage.sprite), "skin:" + sp.GetInstanceID())
                {
                    Group = skins,
                    ValueFunc = () => p._selectedSprite == sp.skinImage.sprite ? Loc.Get("state_selected") : null,
                });
            }
            string actions = Loc.Get("group_actions");
            items.Add(new TextItem(() => Loc.Get("skin_current", SkinName(p._selectedSprite)), "current") { Group = actions });
            UiScan.AddControl(items, p.selectSkinButton, actions, () => Loc.Get("label_confirm_skin"));
            UiScan.AddControl(items, p.closeButton, actions, () => Loc.Get("label_close"));
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_cashier_skin");
        }
    }

    /// <summary>Vehicle shop: current and next vehicle, upgrade price.</summary>
    internal sealed class VehicleContext : ScreenContext
    {
        public override string Id => "vehicle";
        public override int Priority => 63;

        private static VehiclePanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.vehiclePanel : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;
        public override string Title() => Loc.Get("ctx_vehicle");

        public override void Build(List<NavItem> items)
        {
            VehiclePanel p = Panel;
            if (p == null) return;
            LocationTradeManager lt = LocationTradeManager.Instance;
            items.Add(new TextItem(() => Loc.Get("vehicle_current", GameText.VehicleName(lt.currentVehicle), lt.GetMaxTravelPoint(lt.currentVehicle)), "cur"));
            if (lt.currentVehicle != Vehicle.Car)
            {
                Vehicle next = lt.GetNextVehicle(lt.currentVehicle);
                items.Add(new TextItem(() => Loc.Get("vehicle_next", GameText.VehicleName(next), lt.GetMaxTravelPoint(next)), "next"));
            }
            else
            {
                items.Add(new TextItem(Loc.Get("vehicle_best"), "best"));
            }
            UiScan.AddControl(items, p.upgradeButton, string.Empty, () => UiUtil.Text(p.upgradeButtonText));
            items.Add(new TextItem(() => GameText.Money(ResourceManager.Instance.cash), "cash"));
            Transform close = p.transform.Find("Close Button");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>(), string.Empty, () => Loc.Get("label_close"));
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_vehicle");
        }
    }

    /// <summary>
    /// Lottery stand: scratch the hint card (done for you), pick a number, buy tickets, check the draw.
    /// </summary>
    internal sealed class LotteryContext : ScreenContext
    {
        public override string Id => "lottery";
        public override int Priority => 64;

        private static LotteryPanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.lotteryPanel : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;
        public override string Title() => Loc.Get("ctx_lottery");
        public override string Intro() => UiUtil.Text(Panel.vendorMessageText);

        private bool _resultShown;

        public override void OnEnter()
        {
            LotteryPanel p = Panel;
            _resultShown = p != null && p.messageBubble != null && p.messageBubble.activeInHierarchy;
        }

        /// <summary>"Check the results" reveals the winning number a moment later, then shows the vendor's message.</summary>
        public override void Tick()
        {
            LotteryPanel p = Panel;
            if (p == null) return;
            bool shown = p.messageBubble != null && p.messageBubble.activeInHierarchy;
            if (shown && !_resultShown)
            {
                var parts = new List<string> { Loc.Get("lottery_winning", UiUtil.Text(p.winningNumberText)) };
                TMP_Text bubble = p.messageBubble.GetComponentInChildren<TMP_Text>();
                if (bubble != null) parts.Add(UiUtil.Text(bubble));
                foreach (Transform t in p.ticketListContent)
                {
                    var row = t.GetComponent<LotteryTicketRow>();
                    if (row != null && t.gameObject.activeInHierarchy) parts.Add(TextCleaner.Join(", ", UiUtil.Text(row.numberText), UiUtil.Text(row.statusText)));
                }
                parts.Add(Loc.Get("lottery_money", UiUtil.Text(p.currentMoney)));
                SpeechService.Say(TextCleaner.Join(". ", parts.ToArray()), true, MsgCategory.Events);
            }
            _resultShown = shown;
        }

        public override void Build(List<NavItem> items)
        {
            LotteryPanel p = Panel;
            if (p == null) return;
            string scratch = UiUtil.Text(p.scratchHeaderLabel);
            if (!p.isScratchOff)
            {
                items.Add(new ActionItem(() => Loc.Get("lottery_scratch"), () =>
                {
                    p.scratchArea.ForceReveal();
                    p.OnScratchComplete();
                    SpeechService.Say(UiUtil.Text(p.hintText), true);
                }, "scratch") { Group = scratch });
            }
            else
            {
                items.Add(new TextItem(() => UiUtil.Text(p.hintText), "hint") { Group = scratch });
            }
            if (p.buyPanel != null && p.buyPanel.activeInHierarchy)
            {
                string buy = UiUtil.Text(p.buyHeaderLabel);
                items.Add(new ActionItem(() => Loc.Get("lottery_number"), () => SpeechService.Say(Loc.Get("hint_slider"), true), "number")
                {
                    Group = buy,
                    RoleFunc = () => Loc.Get("role_slider"),
                    ValueFunc = () => UiUtil.Text(p.selectNumberText),
                    DetailFunc = () => UiUtil.Text(p.selectHintLabel),
                    OnAdjust = (dir, large) =>
                    {
                        p.ChangeSelectedNumber(dir * (large ? 10 : 1));
                        SpeechService.Say(UiUtil.Text(p.selectNumberText), true);
                        return true;
                    },
                });
                if (p.diceButton != null && p.diceButton.gameObject.activeInHierarchy)
                {
                    items.Add(new ActionItem(() => Loc.Get("lottery_random"), () =>
                    {
                        UiUtil.Click(p.diceButton.gameObject);
                        SpeechService.Say(UiUtil.Text(p.selectNumberText), true);
                    }, "dice") { Group = buy });
                }
                if (p.buyButton != null && p.buyButton.gameObject.activeInHierarchy)
                {
                    items.Add(new ActionItem(() => UiUtil.Text(p.buyButtonLabel), () =>
                    {
                        if (!p.buyButton.interactable)
                        {
                            SpeechService.Say(Loc.Get("not_available_now"), true);
                            return;
                        }
                        string number = UiUtil.Text(p.selectNumberText);
                        string moneyBefore = UiUtil.Text(p.currentMoney);
                        UiUtil.Click(p.buyButton.gameObject);
                        // Not enough money or already bought: the game shows a notice, which is read instead.
                        string moneyAfter = UiUtil.Text(p.currentMoney);
                        if (moneyAfter != moneyBefore) SpeechService.Say(Loc.Get("lottery_bought", number, moneyAfter), true, MsgCategory.Shop);
                    }, "buy") { Group = buy, TargetObject = p.buyButton.gameObject });
                }
                items.Add(new TextItem(() => Loc.Get("lottery_money", UiUtil.Text(p.currentMoney)), "money") { Group = buy });
            }
            if (p.checkPanel != null && p.checkPanel.activeInHierarchy)
            {
                string check = UiUtil.Text(p.checkHeaderLabel);
                items.Add(new TextItem(() => Loc.Get("lottery_winning", UiUtil.Text(p.winningNumberText)), "winning") { Group = check });
                UiScan.AddControl(items, p.checkLotteryButton, check, () => UiUtil.Text(p.checkButtonLabel));
                string history = UiUtil.Text(p.historyHeaderLabel);
                int n = 0;
                foreach (Transform t in p.ticketListContent)
                {
                    var row = t.GetComponent<LotteryTicketRow>();
                    if (row == null || !t.gameObject.activeInHierarchy) continue;
                    LotteryTicketRow r = row;
                    items.Add(new TextItem(() => TextCleaner.Join(", ", UiUtil.Text(r.numberText), UiUtil.Text(r.statusText)), "ticket" + n++) { Group = history });
                }
            }
            if (p.messageBubble != null && p.messageBubble.activeInHierarchy)
            {
                UiScan.AddText(items, p.messageBubble.GetComponentInChildren<TMP_Text>());
            }
            Transform close = p.transform.Find("Close Button");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>(), string.Empty, () => Loc.Get("label_close"));
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_lottery");
        }
    }
}
