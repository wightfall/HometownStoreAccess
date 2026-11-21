using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Game;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    /// <summary>
    /// The shop view with no panel open: a cursor moves along the shop floor. Left/Right step one cell,
    /// Up/Down jump between buildings, Enter opens the building, Tab switches to the toolbar.
    /// </summary>
    internal sealed class FloorContext : ScreenContext
    {
        public static bool ToolbarOpen;

        public override string Id => "floor";
        public override int Priority => 10;
        public override bool UsesListNavigation => false;

        public override bool IsActive()
        {
            GameControl gc = GameControl.Instance;
            return gc != null && !ToolbarOpen && gc.CurrentGameplayMode == GameplayMode.Default && BuildingSystem.Instance != null;
        }

        public override string Title() => Loc.Get("ctx_shop", GameText.Phase());

        public override string Intro()
        {
            FloorCursor.Refresh();
            return FloorCursor.Describe();
        }

        public override void Build(List<NavItem> items) { }

        public override bool HandleInput() => FloorKeys.Handle(allowOpen: true);

        public override string ReadAll()
        {
            FloorCursor.Refresh();
            var parts = new List<string> { GameText.Phase(), GameText.Money(ResourceManager.Instance.cash) };
            foreach (BuildingObject b in BuildingSystem.Instance.allBuildings) parts.Add(GameText.BuildingSummary(b));
            return TextCleaner.Join(". ", parts.ToArray());
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_floor");
        }
    }

    /// <summary>Floor cursor keys shared by the shop view and build mode.</summary>
    internal static class FloorKeys
    {
        public static bool Handle(bool allowOpen)
        {
            bool ctrl = ModInput.Ctrl;
            if (ModInput.Down(KeyCode.LeftArrow))
            {
                if (ctrl ? FloorCursor.JumpToPointOfInterest(-1) : FloorCursor.Step(-1)) AfterMove();
                return true;
            }
            if (ModInput.Down(KeyCode.RightArrow))
            {
                if (ctrl ? FloorCursor.JumpToPointOfInterest(1) : FloorCursor.Step(1)) AfterMove();
                return true;
            }
            if (ModInput.Down(KeyCode.UpArrow))
            {
                if (ctrl) FloorScanner.ChangeCategory(-1);
                else FloorScanner.Step(-1);
                return true;
            }
            if (ModInput.Down(KeyCode.DownArrow))
            {
                if (ctrl) FloorScanner.ChangeCategory(1);
                else FloorScanner.Step(1);
                return true;
            }
            if (ModInput.Down(KeyCode.Home)) { FloorCursor.JumpToEnd(-1); AfterMove(); return true; }
            if (ModInput.Down(KeyCode.End)) { FloorCursor.JumpToEnd(1); AfterMove(); return true; }
            if (ModInput.Down(KeyCode.PageUp)) { if (FloorCursor.ChangeRow(1)) AfterMove(); return true; }
            if (ModInput.Down(KeyCode.PageDown)) { if (FloorCursor.ChangeRow(-1)) AfterMove(); return true; }
            if (ModInput.Down(KeyCode.I))
            {
                FloorCursor.Refresh();
                BuildingObject b = FloorCursor.Building;
                string text;
                if (Placement.Active) text = Placement.DescribeSpot();
                else if (FloorCursor.OnDelivery) text = TextCleaner.Join(". ", Loc.Get("delivery_truck"), Loc.Get("contents", GameText.Contents(ResourceManager.Instance.delivery)));
                else if (b != null) text = GameText.BuildingDetails(b);
                else text = FloorCursor.Describe();
                text = TextCleaner.Join(". ", text, CustomerInfo.AtCursor());
                SpeechService.Say(text, true);
                return true;
            }
            if (allowOpen && (ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter)))
            {
                FloorCursor.Refresh();
                if (FloorCursor.OnDelivery) GameActions.OpenDelivery(FloorCursor.Floor.Delivery);
                else if (FloorCursor.Building != null) GameActions.OpenBuilding(FloorCursor.Building);
                else SpeechService.Say(Loc.Get("nothing_to_open"), true);
                return true;
            }
            if (allowOpen && ModInput.Down(KeyCode.Tab))
            {
                FloorContext.ToolbarOpen = true;
                return true;
            }
            return false;
        }

        private static void AfterMove()
        {
            if (Placement.Active) Placement.Moved();
            else FloorCursor.Announce();
        }
    }

    /// <summary>The HUD buttons as a list (Tab from the shop floor).</summary>
    internal sealed class ToolbarContext : ScreenContext
    {
        public override string Id => "toolbar";
        public override int Priority => 11;

        public override bool IsActive()
        {
            GameControl gc = GameControl.Instance;
            if (gc == null || !FloorContext.ToolbarOpen) return false;
            if (gc.CurrentGameplayMode != GameplayMode.Default)
            {
                FloorContext.ToolbarOpen = false;
                return false;
            }
            return true;
        }

        public override string Title() => Loc.Get("ctx_toolbar");

        public override void Build(List<NavItem> items)
        {
            UIPanelManager ui = UIPanelManager.Instance;
            if (ui == null) return;
            items.Add(new TextItem(StatusText.Short, "status"));
            var added = new HashSet<Selectable>();
            void Add(Selectable s, System.Func<string> label = null)
            {
                if (s == null || added.Contains(s) || !UiUtil.IsShown(s.gameObject)) return;
                added.Add(s);
                items.Add(new ButtonItem(s, label));
            }
            Add(ui.nextPhase, NextPhaseLabel);
            Add(ui.skipDayButton);
            if (ui.phaseMaininterface != null)
            {
                // The game disables the button of the current speed; say "current" rather than "unavailable".
                foreach (Button b in new[] { ui.phaseMaininterface.pauseButton, ui.phaseMaininterface.x1Button, ui.phaseMaininterface.x2Button, ui.phaseMaininterface.x3Button })
                {
                    if (b == null || added.Contains(b) || !UiUtil.IsShown(b.gameObject)) continue;
                    added.Add(b);
                    Button speed = b;
                    items.Add(new ActionItem(() => UiUtil.LabelFor(speed.gameObject), () =>
                    {
                        if (speed.IsInteractable()) UiUtil.Click(speed.gameObject);
                        else Speech.SpeechService.Say(Loc.Get("state_current"), true);
                    }, "speed:" + speed.name)
                    {
                        ValueFunc = () => speed.IsInteractable() ? null : Loc.Get("state_current"),
                        TargetObject = speed.gameObject,
                    });
                }
            }
            if (ui.mainInterface != null)
            {
                foreach (GameObject root in ui.mainInterface)
                {
                    if (root == null || !root.activeInHierarchy) continue;
                    var tmp = new List<NavItem>();
                    UiScan.Add(tmp, root);
                    foreach (NavItem i in tmp)
                    {
                        var sel = i.Target != null ? i.Target.GetComponent<Selectable>() : null;
                        if (sel == null || added.Contains(sel)) continue;
                        if (sel is Toggle && sel.transform.parent != null && sel.transform.parent.GetComponent<MessageBoxPanel>() != null)
                        {
                            added.Add(sel);
                            items.Add(new ToggleItem((Toggle)sel, () => Loc.Get("label_customer_messages")));
                            continue;
                        }
                        added.Add(sel);
                        items.Add(i);
                    }
                }
            }
        }

        public static string NextPhaseLabel()
        {
            GameControl gc = GameControl.Instance;
            if (gc == null) return Loc.Get("label_next_phase");
            switch (gc.CurrentGamePhase)
            {
                case GamePhase.Morning: return Loc.Get("label_open_shop");
                case GamePhase.Noon: return TimeControl.Instance.ReachedEndOfTime() ? Loc.Get("label_go_to_map") : Loc.Get("label_shop_open_wait");
                case GamePhase.Evening: return Loc.Get("label_open_map");
                default: return Loc.Get("label_next_phase");
            }
        }

        public override bool HandleInput()
        {
            if (ModInput.Down(KeyCode.Tab) && !ModInput.Shift)
            {
                FloorContext.ToolbarOpen = false;
                return true;
            }
            return false;
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_toolbar");
        }
    }

    /// <summary>
    /// Build mode: the blueprint list plus the floor cursor for placing, moving and picking up buildings.
    /// Tab switches between the list and the floor. While a building is being placed the arrows move it.
    /// </summary>
    internal sealed class BuildContext : ScreenContext
    {
        private bool _floorMode;
        private GameplayMode _lastMode = GameplayMode.Default;
        private bool _hadTemp;
        private readonly CategoryFilter _filter = new CategoryFilter(
            o => o == Core.CategoryCycle.All ? Loc.Get("cat_all_blueprints") : TradeContext.CategoryLabel(o),
            i => i.Key != null && i.Key.StartsWith("bp:"));

        public override string Id => "build";
        public override int Priority => 40;
        public override bool UsesListNavigation => !_floorMode && !Placement.Active;

        private static BuildingPanel Panel => UIPanelManager.Instance != null && UIPanelManager.Instance.buildingPanelGameObject != null
            ? UIPanelManager.Instance.buildingPanelGameObject.GetComponent<BuildingPanel>() : null;

        public override bool IsActive()
        {
            GameControl gc = GameControl.Instance;
            if (gc == null) return false;
            BuildingPanel p = Panel;
            return gc.CurrentGameplayMode != GameplayMode.Default || (p != null && p.gameObject.activeInHierarchy);
        }

        public override void OnExit()
        {
            if (!IsActive()) _filter.Cycle.Reset();
        }

        public override void OnEnter()
        {
            _floorMode = false;
            _lastMode = GameControl.Instance.CurrentGameplayMode;
            _hadTemp = Placement.Active;
            FloorCursor.Refresh();
        }

        public override string Title() => TextCleaner.Join(". ", Loc.Get("ctx_build"), ModeName());

        public override string Intro()
        {
            if (BuildingSystem.Instance.currentBlueprint.Count == 0) return Loc.Get("build_no_blueprints");
            return null;
        }

        private static string ModeName()
        {
            switch (GameControl.Instance.CurrentGameplayMode)
            {
                case GameplayMode.MoveMode: return Loc.Get("mode_move");
                case GameplayMode.PickupMode: return Loc.Get("mode_pickup");
                default: return Loc.Get("mode_build");
            }
        }

        public override void Tick()
        {
            GameplayMode mode = GameControl.Instance.CurrentGameplayMode;
            if (mode != _lastMode)
            {
                _lastMode = mode;
                if (mode != GameplayMode.Default)
                {
                    if (mode == GameplayMode.MoveMode || mode == GameplayMode.PickupMode) _floorMode = true;
                    // Starting to place or lift a building already announces itself.
                    if (!Placement.Active)
                    {
                        SpeechService.Say(ModeName(), true);
                        if (_floorMode)
                        {
                            FloorCursor.Refresh();
                            SpeechService.Say(FloorCursor.Describe(), false);
                        }
                    }
                }
            }
            bool temp = Placement.Active;
            if (_hadTemp && !temp)
            {
                // Escape (the game's own key) cancelled placing or moving: nothing changed.
                if (Time.unscaledTime - Placement.PlacedAt > 0.5f) SpeechService.Say(Loc.Get("place_cancelled"), true);
            }
            if (_hadTemp && !temp && mode == GameplayMode.BuildMode)
            {
                // The ghost was placed or cancelled: go back to the blueprint list.
                _floorMode = false;
                AccessibilityRoot.Instance.Contexts.Refresh(announceFocus: true);
            }
            _hadTemp = temp;
        }

        public override void Build(List<NavItem> items)
        {
            BuildingSystem bs = BuildingSystem.Instance;
            string bp = Loc.Get("group_blueprints");
            List<BuildingBlueprint> blueprints = bs.currentBlueprint.Where(x => x != null && x.Id != null).ToList();
            _filter.Prepare(blueprints.Select(x => KindOf(x.Id)));
            if (_filter.Active) items.Add(_filter.Item(bp));
            foreach (BuildingBlueprint blueprint in blueprints)
            {
                Building b = blueprint.Id;
                if (!_filter.Matches(KindOf(b))) continue;
                items.Add(new ActionItem(() => Loc.Get("blueprint_entry", GameText.Name(b), BlueprintAmount(b)), () =>
                {
                    if (GameActions.StartPlacing(b)) _floorMode = true;
                }, "bp:" + b.name)
                {
                    Group = bp,
                    DetailFunc = () => BlueprintDetails(b),
                    OnActivateAlt = () => GameActions.SellBlueprint(b),
                });
            }
            BuildingPanel p = Panel;
            if (p == null) return;
            string tools = Loc.Get("group_build_tools");
            UiScan.AddControl(items, p._moveButton, tools, () => Loc.Get("label_move_mode"));
            UiScan.AddControl(items, p._PickupButton, tools, () => Loc.Get("label_pickup_mode"));
            Transform close = p.transform.Find("Close Panel");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>(), tools);
        }

        /// <summary>What a building is, like the game's own build tags (its buildingCategory is the goods it holds).</summary>
        private static string KindOf(Building b)
        {
            if (b is Manufacture) return "Manufacture";
            if (b is Storage) return "Storage";
            if (b is Cashier) return "Cashier";
            if (b is Shelf) return "Shelf";
            return "Decorate";
        }

        private static int BlueprintAmount(Building b)
        {
            BuildingBlueprint bp = BuildingSystem.Instance.currentBlueprint.FirstOrDefault(x => x.Id == b);
            return bp != null ? bp.amount : 0;
        }

        private static string BlueprintDetails(Building b)
        {
            var parts = new List<string> { GameText.Name(b), GameText.Description(b), Loc.Get("building_size", b.sizeX) };
            parts.Add(StoreFloor.RequiredKind(b) == CellKind.BackRoom ? Loc.Get("needs_back_room") : Loc.Get("needs_shop_floor"));
            if (b is Shelf shelf)
            {
                parts.Add(Loc.Get("shelf_slots", shelf.storageSlotLimit));
                parts.Add(Loc.Get("can_hold", string.Join(", ", shelf.storeAbleItems.Select(GameText.Name).ToArray())));
            }
            if (b is Storage st) parts.Add(Loc.Get("storage_adds", st.storageSlotAdd));
            if (b.upkeepCost > 0) parts.Add(Loc.Get("building_upkeep", b.upkeepCost));
            if (!(b is Cashier)) parts.Add(Loc.Get("sell_value", Mathf.RoundToInt(b.marketValue * 0.2f)));
            parts.Add(Loc.Get("hint_blueprint"));
            return TextCleaner.Join(". ", parts.ToArray());
        }

        public override bool HandleInput()
        {
            bool placing = Placement.Active;
            if (!placing && !_floorMode && _filter.HandleKeys()) return true;
            if (!placing && ModInput.Down(KeyCode.Tab))
            {
                _floorMode = !_floorMode;
                if (_floorMode)
                {
                    FloorCursor.Refresh();
                    SpeechService.Say(TextCleaner.Join(". ", Loc.Get("build_floor"), FloorCursor.Describe()), true);
                }
                else
                {
                    SpeechService.Say(Loc.Get("group_blueprints"), true);
                    AccessibilityRoot.Instance.Contexts.AnnounceFocus(includeGroup: false, interrupt: false);
                }
                return true;
            }
            if (ModInput.Down(KeyCode.M) && !placing && (_floorMode || ModInput.Ctrl))
            {
                UiUtil.Click(Panel?._moveButton?.gameObject);
                return true;
            }
            if (!_floorMode && !placing) return false;

            if (FloorKeys.Handle(allowOpen: false)) return true;
            if (ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter))
            {
                if (placing)
                {
                    Placement.TryPlace();
                    return true;
                }
                FloorCursor.Refresh();
                BuildingObject b = FloorCursor.Building;
                if (b == null)
                {
                    SpeechService.Say(Loc.Get("build_nothing_here"), true);
                    return true;
                }
                if (GameControl.Instance.CurrentGameplayMode == GameplayMode.PickupMode) GameActions.PickUp(b);
                else GameActions.StartMoving(b);
                return true;
            }
            if (ModInput.Down(KeyCode.Delete) || ModInput.Down(KeyCode.Backspace))
            {
                if (placing) return true;
                FloorCursor.Refresh();
                BuildingObject b = FloorCursor.Building;
                if (b == null) SpeechService.Say(Loc.Get("build_nothing_here"), true);
                else GameActions.PickUp(b);
                return true;
            }
            return true;
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get("help_build");
        }
    }

    /// <summary>Summaries of customers in the shop.</summary>
    internal static class CustomerInfo
    {
        public static List<CustomerObject> All()
        {
            var list = new List<CustomerObject>();
            if (CustomerSpawnSystem.Instance == null) return list;
            foreach (CustomerObject c in CustomerSpawnSystem.Instance.customerOnScreen)
            {
                if (c != null) list.Add(c);
            }
            return list.OrderBy(c => c.transform.position.x).ToList();
        }

        public static string Describe(CustomerObject c, bool relativeToCursor)
        {
            var parts = new List<string> { GameText.CustomerName(c), GameText.CustomerState(c) };
            if (c.NeedItems != null && c.NeedItems.Count > 0 && c.currentCustomerState == global::CustomerState.TakeProduct)
            {
                parts.Add(Loc.Get("customer_wants", string.Join(", ", c.NeedItems.Select(kv => Loc.Get("item_stack", GameText.Name(kv.Key), kv.Value)).ToArray())));
            }
            if (c.inventoryBasket != null && c.inventoryBasket.ItemList.Count > 0)
            {
                parts.Add(Loc.Get("customer_basket", GameText.Contents(c.inventoryBasket)));
            }
            if (relativeToCursor && BuildingSystem.Instance != null)
            {
                int cx = StoreFloor.CellXOf(c.transform.position);
                int d = cx - FloorCursor.X;
                parts.Add(d == 0 ? Loc.Get("here") : d < 0 ? Loc.Get("cells_left", -d) : Loc.Get("cells_right", d));
            }
            return TextCleaner.Join(", ", parts.ToArray());
        }

        public static string Summary()
        {
            List<CustomerObject> all = All();
            if (all.Count == 0) return Loc.Get("no_customers");
            var parts = new List<string> { Loc.Get("customer_count", all.Count) };
            foreach (CustomerObject c in all) parts.Add(Describe(c, true));
            return TextCleaner.Join(". ", parts.ToArray());
        }

        public static string AtCursor()
        {
            var parts = new List<string>();
            foreach (CustomerObject c in StoreFloor.CustomersAt(FloorCursor.X)) parts.Add(Describe(c, false));
            return parts.Count == 0 ? string.Empty : TextCleaner.Join(". ", parts.ToArray());
        }
    }

    /// <summary>Phase, day, time and money in one line.</summary>
    internal static class StatusText
    {
        public static string Short()
        {
            var parts = new List<string>();
            TimeControl tc = TimeControl.Instance;
            if (tc != null) parts.Add(Loc.Get("day_n", tc.DayPlayerPlay));
            parts.Add(GameText.Phase());
            if (GameControl.Instance != null && GameControl.Instance.CurrentGamePhase == GamePhase.Noon && tc != null)
            {
                if (tc.ReachedEndOfTime()) parts.Add(Loc.Get("time_shop_closed"));
                else if (tc.ReachedRushTime()) parts.Add(Loc.Get("time_closing_customers", CustomerSpawnSystem.Instance.customerOnScreen.Count));
                else
                {
                    int pct = tc.maxTickTimeDaylight > 0 ? Mathf.Clamp(Mathf.RoundToInt(100f * tc.TickTimeGame / tc.maxTickTimeDaylight), 0, 100) : 0;
                    parts.Add(Loc.Get("time_percent", pct));
                }
                parts.Add(SpeedText());
            }
            if (GameControl.Instance != null && GameControl.Instance.CurrentGamePhase == GamePhase.Evening && LocationTradeManager.Instance != null)
            {
                LocationTradeManager lt = LocationTradeManager.Instance;
                parts.Add(Loc.Get("travel_status", lt.currentTravelPoint, lt.maxTravelPoint, GameText.VehicleName(lt.currentVehicle)));
            }
            if (ResourceManager.Instance != null) parts.Add(GameText.Money(ResourceManager.Instance.cash));
            // The game shows the shop's level only through its size; customers' trust needs it.
            if (StoreManager.Instance != null) parts.Add(Loc.Get("status_shop_level", StoreManager.Instance.StoreLevel + 1));
            return TextCleaner.Join(", ", parts.ToArray());
        }

        public static string SpeedText()
        {
            float s = Time.timeScale;
            TimeControl tc = TimeControl.Instance;
            if (s == 0f) return Loc.Get("speed_paused");
            if (tc == null) return string.Empty;
            if (Mathf.Approximately(s, tc.X1TimeScale)) return Loc.Get("speed_x1");
            if (Mathf.Approximately(s, tc.X2TimeScale)) return Loc.Get("speed_x2");
            if (Mathf.Approximately(s, tc.X3TimeScale)) return Loc.Get("speed_x3");
            return Loc.Get("speed_other", s.ToString("0.#"));
        }
    }
}
