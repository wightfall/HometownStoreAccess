using System.Collections.Generic;
using System.Linq;
using Asset.Scripts.Map;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Game;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    /// <summary>
    /// The evening travel map. Places are listed with distance and direction; Enter shows a place's
    /// details and focuses its Travel button. W toggles walk mode, where the arrow keys move your
    /// token one tile at a time along the roads.
    /// </summary>
    internal sealed class MapContext : ScreenContext
    {
        private bool _walkMode;
        private int _lastIndex = -1;
        private bool _wasMoving;
        private string _lastInfoKey;
        private Content_Map _lastMap;
        // The place whose details were opened last (its list key).
        private string _detailsNode;

        public override string Id => "map";
        public override int Priority => 60;
        public override bool UsesListNavigation => !_walkMode;

        private static SelectLocationPanel Panel => UIPanelManager.Instance != null ? UIPanelManager.Instance.GetSelectLocationPanel() : null;
        private static Content_Map CurrentMap => Panel != null ? Panel.currentMap : null;
        private static MapBoardNavigation Board => CurrentMap != null ? CurrentMap.mapBoardNavigation : null;

        public override bool IsActive() => Panel != null && Panel.gameObject.activeInHierarchy;

        public override void OnEnter()
        {
            _walkMode = false;
            _lastIndex = Board != null ? Board._currentIndex : -1;
            _wasMoving = false;
            _lastInfoKey = Panel?.previousShowTarget;
            _lastMap = CurrentMap;
            _detailsNode = null;
        }

        public override string Title()
        {
            Content_Map m = CurrentMap;
            return TextCleaner.Join(". ", Loc.Get("ctx_map", m != null ? MapName(m) : "?"), TravelPoints());
        }

        private static string MapName(Content_Map m) => TextCleaner.Clean(m.detailsKey.GetName(m.name));

        private static string TravelPoints()
        {
            LocationTradeManager lt = LocationTradeManager.Instance;
            return lt == null ? string.Empty : Loc.Get("travel_points", lt.currentTravelPoint, lt.maxTravelPoint, GameText.VehicleName(lt.currentVehicle));
        }

        private static IEnumerable<Content_SelectForMap> Nodes()
        {
            Content_Map m = CurrentMap;
            if (m == null) yield break;
            foreach (Content_SelectForMap n in m.GetComponentsInChildren<Content_SelectForMap>(false))
            {
                if (n.gameObject.activeInHierarchy) yield return n;
            }
        }

        private static HashSet<GridPoint> Walkable(MapBoardNavigation b)
        {
            var set = new HashSet<GridPoint>();
            if (b == null) return set;
            foreach (Vector2Int c in b.pathCoordinates) set.Add(new GridPoint(c.x, c.y));
            return set;
        }

        private static GridPoint? TokenCoord(MapBoardNavigation b)
        {
            if (b == null || b._currentIndex < 0 || b._currentIndex >= b.pathCoordinates.Count) return null;
            Vector2Int c = b.pathCoordinates[b._currentIndex];
            return new GridPoint(c.x, c.y);
        }

        private static bool TokenOn(Content_SelectForMap n)
        {
            GridPoint? at = TokenCoord(Board);
            return at.HasValue && n != null && n.spawnCoordinate.x == at.Value.X && n.spawnCoordinate.y == at.Value.Y;
        }

        private static string NodeKey(Content_SelectForMap n) => "node:" + n.GetInstanceID();

        /// <summary>The list key of the place the token stands on, or null.</summary>
        private static string HereKey()
        {
            Content_SelectForMap here = Nodes().FirstOrDefault(TokenOn);
            return here != null ? NodeKey(here) : null;
        }

        /// <summary>
        /// When the focused item goes away because a place's details closed (Escape, or arriving somewhere
        /// that opens no screen), the focus goes back to that place, not to the next item in the list
        /// (which could be End of the day). Otherwise to the place you are at.
        /// </summary>
        public override string PreferredFocusKey()
        {
            if (_detailsNode != null && Nodes().Any(n => NodeKey(n) == _detailsNode)) return _detailsNode;
            return HereKey();
        }

        /// <summary>Coming back to the map (a shop closed): start on the place you are at.</summary>
        public override int InitialFocus(IReadOnlyList<NavItem> items)
        {
            string key = HereKey();
            if (key == null) return -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Key == key) return i;
            }
            return -1;
        }

        private static string NodeName(Content_SelectForMap n)
        {
            if (n is Content_SelectLocation loc && loc.LocationID != null) return GameText.Name(loc.LocationID);
            if (n is Content_SelectSign sign && sign.TargetMap != null) return Loc.Get("road_to", MapName(sign.TargetMap));
            if (n.name.IndexOf("shop", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.name.IndexOf("store", System.StringComparison.OrdinalIgnoreCase) >= 0) return Loc.Get("map_your_shop");
            return TextCleaner.HumanizeName(n.name);
        }

        private static string NodeMarkers(Content_SelectForMap n)
        {
            var parts = new List<string>();
            if (n is Content_SelectLocation loc && loc.LocationID != null)
            {
                LocationTradeManager lt = LocationTradeManager.Instance;
                LocationProfile prof = lt.GetLocationProfile(loc.LocationID);
                // (Standing on it, the distance already says "you are here".)
                if (prof != null && lt.CurrentAtLocationProfile == prof && !TokenOn(n)) parts.Add(Loc.Get("map_you_are_here"));
                foreach (Journal j in EventJournalManager.Instance.CurrentJournals)
                {
                    if (j != null && j.showToPlayer && j.haveLocationCondition && j.locationTradeThatTrigger == loc.LocationID) { parts.Add(Loc.Get("map_quest")); break; }
                }
                if (!lt.locationAlreadyNotify.Contains(loc.LocationID)) parts.Add(Loc.Get("map_new"));
                if (loc.LocationID.lotteryPanel) parts.Add(Loc.Get("map_lottery"));
                if (loc.LocationID.vehiclePanel) parts.Add(Loc.Get("map_vehicle_shop"));
            }
            else if (n is Content_SelectSign sign && sign.TargetMap != null)
            {
                string there = SignMarker(sign.TargetMap);
                if (there != null) parts.Add(there);
            }
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// The game marks a road sign when a place in the area it leads to has an event or is new (the
        /// first such place decides, as in the game).
        /// </summary>
        private static string SignMarker(Content_Map target)
        {
            SelectLocationPanel p = Panel;
            if (p == null || p.allSelectLocation == null || !p.allSelectLocation.TryGetValue(target, out List<Content_SelectForMap> places)) return null;
            LocationTradeManager lt = LocationTradeManager.Instance;
            foreach (Content_SelectForMap place in places)
            {
                if (!(place is Content_SelectLocation loc) || loc.LocationID == null || !loc.gameObject.activeSelf) continue;
                foreach (Journal j in EventJournalManager.Instance.CurrentJournals)
                {
                    if (j != null && j.showToPlayer && j.haveLocationCondition && j.locationTradeThatTrigger == loc.LocationID) return Loc.Get("map_sign_event");
                }
                if (!lt.locationAlreadyNotify.Contains(loc.LocationID)) return Loc.Get("map_sign_new");
            }
            return null;
        }

        private static string Distance(Content_SelectForMap n)
        {
            MapBoardNavigation b = Board;
            GridPoint? from = TokenCoord(b);
            if (from == null) return string.Empty;
            int? steps = MapGeometry.StepsBetween(Walkable(b), from.Value, new GridPoint(n.spawnCoordinate.x, n.spawnCoordinate.y));
            string dir = null;
            if (b.playerToken != null)
            {
                Vector3 d = n.transform.position - b.playerToken.position;
                string key = MapGeometry.CompassKey(d.x, d.y, 20);
                if (key != null) dir = Loc.Get(key);
            }
            if (steps == 0) return Loc.Get("map_here");
            return TextCleaner.Join(", ", steps.HasValue ? Loc.Get("map_steps", steps.Value) : null, dir);
        }

        public override void Build(List<NavItem> items)
        {
            SelectLocationPanel p = Panel;
            if (p == null) return;
            string places = Loc.Get("group_places");
            foreach (Content_SelectForMap n in Nodes())
            {
                Content_SelectForMap node = n;
                items.Add(new ActionItem(() => NodeName(node), () => SelectNode(node), NodeKey(node))
                {
                    Group = places,
                    ValueFunc = () => TextCleaner.Join(", ", NodeMarkers(node), Distance(node)),
                    DetailFunc = () => Loc.Get("hint_map_node"),
                    TargetObject = node.gameObject,
                });
            }

            if (p.infoPanel != null && p.infoPanel.activeInHierarchy && !string.IsNullOrEmpty(p.previousShowTarget))
            {
                string info = UiUtil.Text(p.titleProfile);
                UiScan.AddText(items, p.descriptionProfile, info);
                // The game lists the events of this place by name; some events have no name in any
                // language and leave an empty line, so the mod names them.
                string events = EventsLine(p);
                if (events != null) items.Add(new TextItem(events, "events_here") { Group = info });
                LocationProfile prof = InfoProfile(p);
                if (prof != null)
                {
                    foreach (TradeItemSet t in prof.currentItemList.Where(t => t.item != null))
                    {
                        TradeItemSet set = t;
                        items.Add(new TextItem(() => Loc.Get("goods_line", GameText.Name(set.item), set.amount, Mathf.CeilToInt(set.pricePerUnit)), "good:" + set.item.name, () => GameText.ItemDetails(set.item)) { Group = info });
                    }
                    foreach (TradeShelfSet t in prof.currentShelfList.Where(t => t.shelf != null))
                    {
                        TradeShelfSet set = t;
                        items.Add(new TextItem(() => Loc.Get("goods_line", GameText.Name(set.shelf), set.amount, Mathf.CeilToInt(set.pricePerUnit)), "goodb:" + set.shelf.name) { Group = info });
                    }
                }
                LocationTradeManager ltm = LocationTradeManager.Instance;
                if (prof != null && ltm != null && ltm.CurrentAtLocationProfile == prof && p.travelToLocationButton != null)
                {
                    // Already here (the shop was closed with Escape): the game's Travel button does nothing
                    // when the player does not move, so open the place again directly (it costs no travel point).
                    LocationProfile here = prof;
                    items.Add(new ActionItem(() => Loc.Get("map_go_in_again"), () => ltm.ReadLocationProfile(here.locationTradeID), "travel_here")
                    {
                        Group = info,
                        TargetObject = p.travelToLocationButton.gameObject,
                    });
                }
                else
                {
                    UiScan.AddControl(items, p.travelToLocationButton, info);
                }
            }

            string map = Loc.Get("group_map");
            items.Add(new TextItem(TravelPoints, "points") { Group = map });
            items.Add(new TextItem(PurchasedToday, "basket") { Group = map });
            UiScan.AddControl(items, p.endOfTheDayButton, map);
            Transform close = p.transform.Find("CloseMap");
            if (close != null) UiScan.AddControl(items, close.GetComponent<Button>(), map, () => Loc.Get("label_close_map"));
            Transform hud = p.transform.Find("MainButton (1)");
            if (hud != null) UiScan.Add(items, hud.gameObject, map);
            Transform stock = p.transform.Find("StorageSummary/StorageButton");
            if (stock != null && stock.gameObject.activeInHierarchy) UiScan.AddControl(items, stock.GetComponent<Button>(), map, () => Loc.Get("label_storage_summary"));
        }

        /// <summary>"Events here: ..." for the details panel, or null.</summary>
        private static string EventsLine(SelectLocationPanel p)
        {
            LocationProfile prof = InfoProfile(p);
            if (prof != null)
            {
                List<string> events = EventsAt(prof.locationTradeID);
                return events.Count > 0 ? Loc.Get("map_quests_here", string.Join(", ", events.ToArray())) : null;
            }
            string signText = UiUtil.Text(p.journalDisplay);
            return signText.Length > 0 ? Loc.Get("map_quests_here", signText) : null;
        }

        /// <summary>The events whose task is to go to this place, as the game's info panel lists them.</summary>
        private static List<string> EventsAt(LocationTrade place)
        {
            var names = new List<string>();
            EventJournalManager ej = EventJournalManager.Instance;
            if (ej == null || place == null) return names;
            foreach (Journal j in ej.CurrentJournals)
            {
                if (j != null && j.haveLocationCondition && j.locationTradeThatTrigger == place) names.Add(GameText.Name(j));
            }
            return names;
        }

        private static LocationProfile InfoProfile(SelectLocationPanel p)
        {
            LocationTradeManager lt = LocationTradeManager.Instance;
            foreach (LocationProfile prof in lt.locationProfilesList)
            {
                if (prof.locationTradeID != null && prof.locationTradeID.name == p.previousShowTarget) return prof;
            }
            return null;
        }

        private static string PurchasedToday()
        {
            LocationTradeManager lt = LocationTradeManager.Instance;
            var parts = new List<string>();
            foreach (var kv in lt.record_basketItem) parts.Add(Loc.Get("item_stack", GameText.Name(kv.Key), kv.Value));
            foreach (var kv in lt.record_basketShelf) parts.Add(Loc.Get("item_stack", GameText.Name(kv.Key), kv.Value));
            return parts.Count == 0 ? Loc.Get("purchased_nothing") : Loc.Get("purchased_today", string.Join(", ", parts.ToArray()));
        }

        private void SelectNode(Content_SelectForMap node)
        {
            if (Board != null && Board._isMoving)
            {
                SpeechService.Say(Loc.Get("map_traveling"), true);
                return;
            }
            bool place = (node is Content_SelectLocation loc && loc.LocationID != null) || (node is Content_SelectSign sign && sign.TargetMap != null);
            if (!place)
            {
                // Your own shop is only a marker on the map: the game does nothing when it is chosen.
                SpeechService.Say(Loc.Get("map_your_shop_hint"), true);
                return;
            }
            _detailsNode = NodeKey(node);
            UiUtil.Click(node.gameObject);
        }

        public override void Tick()
        {
            SelectLocationPanel p = Panel;
            MapBoardNavigation b = Board;
            if (p == null || b == null) return;
            // A road took the player to another map: say where they are now.
            Content_Map map = CurrentMap;
            if (map != _lastMap)
            {
                _lastMap = map;
                _detailsNode = null;
                _lastIndex = b._currentIndex;
                _wasMoving = b._isMoving;
                AudioCues.Play(Cue.Arrive);
                SpeechService.Say(Title(), false, MsgCategory.Events);
                // The places of the old map are gone: start where you stand in the new one (the road
                // sign you came by), as after every other trip.
                ContextManager cm = AccessibilityRoot.Instance.Contexts;
                cm.Refresh(false);
                int here = InitialFocus(cm.Items);
                cm.MoveToSilently(here >= 0 ? here : 0);
                return;
            }
            // Steps of the token while it walks.
            if (b._currentIndex != _lastIndex)
            {
                _lastIndex = b._currentIndex;
                if (b._isMoving || _wasMoving) AudioCues.Play(Cue.Step, PanOf(b.playerToken));
            }
            if (_wasMoving && !b._isMoving)
            {
                AudioCues.Play(Cue.Arrive);
                GridPoint? at = TokenCoord(b);
                Content_SelectForMap here = at.HasValue ? Nodes().FirstOrDefault(n => n.spawnCoordinate.x == at.Value.X && n.spawnCoordinate.y == at.Value.Y) : null;
                if (here != null && !_walkMode) SpeechService.Say(Loc.Get("map_arrived", NodeName(here)), false, MsgCategory.Events);
            }
            if (!_wasMoving && b._isMoving && !_walkMode) SpeechService.Say(Loc.Get("map_traveling"), true);
            _wasMoving = b._isMoving;

            // Info panel for a newly selected place: read it and focus Travel.
            string key = p.infoPanel != null && p.infoPanel.activeInHierarchy ? p.previousShowTarget : null;
            if (key != _lastInfoKey)
            {
                _lastInfoKey = key;
                if (key != null)
                {
                    SpeechService.Say(TextCleaner.Join(". ", UiUtil.Text(p.titleProfile), UiUtil.Text(p.descriptionProfile), EventsLine(p), UiUtil.Text(p.travelToLocationButton.GetComponentInChildren<TMPro.TMP_Text>())), true);
                    FocusTravel();
                }
            }
        }

        private static float PanOf(RectTransform rt)
        {
            if (rt == null) return 0f;
            return Mathf.Clamp(rt.position.x / Mathf.Max(1, Screen.width) * 2f - 1f, -1f, 1f);
        }

        private static void FocusTravel()
        {
            ContextManager cm = AccessibilityRoot.Instance.Contexts;
            cm.Refresh(false);
            SelectLocationPanel p = Panel;
            for (int i = 0; i < cm.Items.Count; i++)
            {
                if (cm.Items[i].Target == p.travelToLocationButton.gameObject)
                {
                    cm.MoveToSilently(i);
                    return;
                }
            }
        }

        public override bool HandleInput()
        {
            if (ModInput.Down(KeyCode.W) && (_walkMode || ModInput.Ctrl))
            {
                _walkMode = !_walkMode;
                SpeechService.Say(_walkMode ? Loc.Get("walk_on", Here()) : Loc.Get("walk_off"), true);
                return true;
            }
            if (!_walkMode) return false;
            MapBoardNavigation b = Board;
            if (b == null) return true;
            if (ModInput.Down(KeyCode.UpArrow)) Walk(b, 0, 1);
            else if (ModInput.Down(KeyCode.DownArrow)) Walk(b, 0, -1);
            else if (ModInput.Down(KeyCode.LeftArrow)) Walk(b, -1, 0);
            else if (ModInput.Down(KeyCode.RightArrow)) Walk(b, 1, 0);
            else if (ModInput.Down(KeyCode.Return) || ModInput.Down(KeyCode.KeypadEnter))
            {
                Content_SelectForMap here = NodeAtToken(b);
                if (here == null) SpeechService.Say(Loc.Get("walk_no_place"), true);
                else
                {
                    _walkMode = false;
                    SelectNode(here);
                }
            }
            else if (ModInput.Down(KeyCode.I)) SpeechService.Say(Here(), true);
            return true;
        }

        private static Content_SelectForMap NodeAtToken(MapBoardNavigation b)
        {
            GridPoint? at = TokenCoord(b);
            if (!at.HasValue) return null;
            return Nodes().FirstOrDefault(n => n.spawnCoordinate.x == at.Value.X && n.spawnCoordinate.y == at.Value.Y);
        }

        private static string Here()
        {
            MapBoardNavigation b = Board;
            Content_SelectForMap n = b != null ? NodeAtToken(b) : null;
            if (n != null) return Loc.Get("walk_at_place", NodeName(n));
            // Nearest place by path distance.
            GridPoint? from = TokenCoord(b);
            if (!from.HasValue) return string.Empty;
            HashSet<GridPoint> walk = Walkable(b);
            Content_SelectForMap best = null;
            int bestSteps = int.MaxValue;
            foreach (Content_SelectForMap node in Nodes())
            {
                int? s = MapGeometry.StepsBetween(walk, from.Value, new GridPoint(node.spawnCoordinate.x, node.spawnCoordinate.y));
                if (s.HasValue && s.Value < bestSteps) { bestSteps = s.Value; best = node; }
            }
            return best != null ? Loc.Get("walk_nearest", NodeName(best), Distance(best)) : Loc.Get("walk_on_road");
        }

        private static void Walk(MapBoardNavigation b, int wantX, int wantY)
        {
            if (b._isMoving) return;
            GridPoint? at = TokenCoord(b);
            if (!at.HasValue) return;
            HashSet<GridPoint> walk = Walkable(b);
            List<GridPoint> neighbours = MapGeometry.NeighboursOf(walk, at.Value);
            var offsets = new List<(double dx, double dy)>();
            Vector3 here = b.pathNodes[b._currentIndex].position;
            foreach (GridPoint g in neighbours)
            {
                int idx = b.pathCoordinates.IndexOf(new Vector2Int(g.X, g.Y));
                Vector3 there = idx >= 0 && idx < b.pathNodes.Count ? b.pathNodes[idx].position : here;
                offsets.Add((there.x - here.x, there.y - here.y));
            }
            int pick = MapGeometry.BestMatch(offsets, wantX, wantY);
            if (pick < 0)
            {
                AudioCues.Play(Cue.Edge, PanOf(b.playerToken));
                SpeechService.Say(Loc.Get("walk_blocked"), true);
                return;
            }
            GridPoint target = neighbours[pick];
            b.MoveToCoordinate(target.X, target.Y);
            Content_SelectForMap n = Nodes().FirstOrDefault(x => x.spawnCoordinate.x == target.X && x.spawnCoordinate.y == target.Y);
            if (n != null)
            {
                AudioCues.Play(Cue.Beacon, PanOf(b.playerToken));
                SpeechService.Say(Loc.Get("walk_at_place", NodeName(n)), true);
            }
            else if (MapGeometry.NeighboursOf(walk, target).Count > 2)
            {
                SpeechService.Say(Loc.Get("walk_crossroads"), true);
            }
        }

        public override IEnumerable<string> HelpLines()
        {
            yield return Loc.Get(_walkMode ? "help_walk" : "help_map");
        }
    }

}
