using System.Collections.Generic;
using System.Linq;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess.Game
{
    /// <summary>
    /// Shop-floor scanner. Things on the floor are grouped in categories (everything, shelves,
    /// storage and machines, cashiers, decorations, customers, free spots, delivery truck).
    /// Up/Down select the previous/next thing of the category and walk the cursor to it;
    /// Ctrl+Up/Down change category. Left/Right still walk one cell at a time.
    /// </summary>
    internal static class FloorScanner
    {
        public enum Category { All, Shelves, StorageAndMachines, Cashiers, Decorations, Customers, FreeSpots, Delivery }

        private static readonly Category[] Order = (Category[])System.Enum.GetValues(typeof(Category));
        private static Category _category = Category.All;
        private static string _lastKey;

        public static Category Current => _category;

        private struct Target
        {
            public string Key;
            public int X;
            public int Y;
            public string Label;
        }

        public static string CategoryName(Category c) => Loc.Get("scan_" + c.ToString().ToLowerInvariant());

        public static void ChangeCategory(int direction)
        {
            int i = System.Array.IndexOf(Order, _category);
            _category = Order[((i + direction) % Order.Length + Order.Length) % Order.Length];
            _lastKey = null;
            List<Target> t = Targets();
            AudioCues.Play(Cue.Wrap);
            SpeechService.Say(TextCleaner.Join(", ", CategoryName(_category), Loc.Get("scan_count", t.Count)), true);
        }

        /// <summary>Moves to the next (+1) or previous (-1) thing of the category, relative to the cursor.</summary>
        public static void Step(int direction)
        {
            FloorCursor.Refresh();
            List<Target> targets = Targets();
            if (targets.Count == 0)
            {
                AudioCues.Play(Cue.Edge);
                SpeechService.Say(Loc.Get("scan_none", CategoryName(_category)), true);
                return;
            }
            int current = _lastKey != null ? targets.FindIndex(t => t.Key == _lastKey) : -1;
            int next;
            if (current >= 0 && targets[current].X == FloorCursor.X)
            {
                next = current + direction;
            }
            else
            {
                // Cursor moved by hand: continue from its position.
                next = direction > 0 ? targets.FindIndex(t => t.X > FloorCursor.X) : targets.FindLastIndex(t => t.X < FloorCursor.X);
                if (next < 0) next = direction > 0 ? targets.Count : -1;
            }
            bool wrapped = false;
            if (next >= targets.Count) { next = 0; wrapped = true; }
            if (next < 0) { next = targets.Count - 1; wrapped = true; }
            Target target = targets[next];
            _lastKey = target.Key;
            FloorCursor.SetPosition(target.X, target.Y);
            AudioCues.Play(wrapped ? Cue.Wrap : Cue.BuildingHere, FloorNav.Pan(FloorCursor.Row, FloorCursor.X));
            if (Placement.Active) Placement.Moved();
            else SpeechService.Say(TextCleaner.Join(", ", target.Label, Loc.Get("position_of", next + 1, targets.Count)), true);
        }

        private static List<Target> Targets()
        {
            var list = new List<Target>();
            StoreFloor floor = FloorCursor.Floor;
            foreach (FloorRow row in floor.Rows)
            {
                string lastOccupant = null;
                int freeStart = int.MinValue;
                foreach (FloorCell cell in row.Cells)
                {
                    if (cell.Kind == CellKind.Special)
                    {
                        if (_category == Category.All || _category == Category.Delivery)
                            list.Add(new Target { Key = "delivery", X = cell.X, Y = row.Y, Label = Loc.Get("delivery_truck") });
                        continue;
                    }
                    if (cell.Occupant != null)
                    {
                        freeStart = int.MinValue;
                        if (cell.Occupant == lastOccupant) continue;
                        lastOccupant = cell.Occupant;
                        BuildingObject b = floor.BuildingAt(row, cell.X);
                        if (b == null || !Matches(b)) continue;
                        list.Add(new Target { Key = cell.Occupant, X = cell.X, Y = row.Y, Label = GameText.BuildingSummary(b) });
                        continue;
                    }
                    lastOccupant = null;
                    if (_category == Category.FreeSpots && cell.Kind != CellKind.None)
                    {
                        if (freeStart == int.MinValue)
                        {
                            freeStart = cell.X;
                            int length = 0;
                            for (int x = cell.X; row.At(x) != null && row.At(x).Occupant == null && row.At(x).Kind == cell.Kind; x++) length++;
                            string where = cell.Kind == CellKind.BackRoom ? Loc.Get("cell_back_room") : Loc.Get("cell_shop_floor");
                            list.Add(new Target { Key = "free:" + row.Y + ":" + cell.X, X = cell.X, Y = row.Y, Label = Loc.Get("scan_free_spot", where, length) });
                        }
                    }
                    else
                    {
                        freeStart = int.MinValue;
                    }
                }
            }
            if (_category == Category.All || _category == Category.Customers)
            {
                foreach (CustomerObject c in Contexts.CustomerInfo.All())
                {
                    int x = StoreFloor.CellXOf(c.transform.position);
                    FloorRow row = FloorCursor.Row;
                    if (row == null) continue;
                    x = FloorNav.Clamp(row, x);
                    list.Add(new Target { Key = "cust:" + c.GetInstanceID(), X = x, Y = row.Y, Label = Contexts.CustomerInfo.Describe(c, false) });
                }
            }
            return list.OrderBy(t => t.Y).ThenBy(t => t.X).ThenBy(t => t.Key).ToList();
        }

        private static bool Matches(BuildingObject b)
        {
            switch (_category)
            {
                case Category.All: return true;
                case Category.Shelves: return b is ShelfObject;
                case Category.StorageAndMachines: return b is StorageObject || b is ManufactureObject;
                case Category.Cashiers: return b is CashierObject;
                case Category.Decorations: return !(b is ShelfObject) && !(b is StorageObject) && !(b is ManufactureObject) && !(b is CashierObject);
                default: return false;
            }
        }
    }
}
