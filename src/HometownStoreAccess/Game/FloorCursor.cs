using System.Collections.Generic;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess.Game
{
    /// <summary>
    /// The keyboard cursor on the shop floor. Shared by the normal shop view and build mode so the
    /// position is kept when switching. Moves cell by cell or building by building, speaks what is
    /// there, plays a panned cue, and keeps the camera on it.
    /// </summary>
    internal static class FloorCursor
    {
        private static bool _initialized;
        private static float _lastMoveTime = -10f;

        public static int X { get; private set; }
        public static int Y { get; private set; }
        public static StoreFloor Floor { get; private set; } = new StoreFloor();

        /// <summary>True for a short time after the cursor moved (used to satisfy the "move the camera" tutorial step).</summary>
        public static bool MovedRecently => Time.unscaledTime - _lastMoveTime < 0.6f;

        public static FloorRow Row
        {
            get
            {
                FloorRow r = Floor.RowAt(Y);
                if (r == null && Floor.Rows.Count > 0) r = Floor.Rows[Floor.Rows.Count - 1];
                return r;
            }
        }

        public static void Reset() => _initialized = false;

        /// <summary>Re-reads the floor; puts the cursor somewhere sensible the first time.</summary>
        public static void Refresh()
        {
            Floor = StoreFloor.Capture();
            if (Floor.Rows.Count == 0) return;
            if (!_initialized || Floor.RowAt(Y) == null)
            {
                FloorRow bottom = Floor.Rows[Floor.Rows.Count - 1];
                Y = bottom.Y;
                FloorCell first = null;
                foreach (FloorCell c in bottom.Cells)
                {
                    if (c.Kind == CellKind.ShopFloor) { first = c; break; }
                }
                X = first != null ? first.X : bottom.MinX;
                _initialized = true;
            }
            X = FloorNav.Clamp(Row, X);
        }

        public static FloorCell Cell => Row?.At(X);
        public static BuildingObject Building => Floor.BuildingAt(Row, X);
        public static bool OnDelivery => Cell != null && Cell.Kind == CellKind.Special;

        public static Vector3 WorldPosition => StoreFloor.CellCenter(X, Y);

        public static void SetPosition(int x, int y)
        {
            Y = y;
            X = FloorNav.Clamp(Row, x);
            _lastMoveTime = Time.unscaledTime;
            FollowCamera();
        }

        /// <summary>Moves one cell; returns false at the edge (after playing the edge cue).</summary>
        public static bool Step(int direction)
        {
            Refresh();
            int nx = FloorNav.Step(Row, X, direction);
            if (nx == X)
            {
                AudioCues.Play(Cue.Edge, FloorNav.Pan(Row, X));
                return false;
            }
            X = nx;
            Moved();
            return true;
        }

        public static bool JumpToPointOfInterest(int direction)
        {
            Refresh();
            int? nx = FloorNav.NextPointOfInterest(Row, X, direction);
            if (!nx.HasValue)
            {
                AudioCues.Play(Cue.Edge, FloorNav.Pan(Row, X));
                return false;
            }
            X = nx.Value;
            Moved();
            return true;
        }

        public static void JumpToEnd(int direction)
        {
            Refresh();
            FloorRow r = Row;
            if (r == null) return;
            X = direction < 0 ? r.MinX : r.MaxX;
            Moved();
        }

        public static bool ChangeRow(int direction)
        {
            Refresh();
            int i = Floor.Rows.IndexOf(Row);
            // Rows are stored top to bottom; "up" means a smaller index.
            int ni = i - direction;
            if (i < 0 || ni < 0 || ni >= Floor.Rows.Count)
            {
                AudioCues.Play(Cue.Edge);
                return false;
            }
            Y = Floor.Rows[ni].Y;
            X = FloorNav.Clamp(Row, X);
            Moved();
            return true;
        }

        private static void Moved()
        {
            _lastMoveTime = Time.unscaledTime;
            FollowCamera();
            FloorCell c = Cell;
            float pan = FloorNav.Pan(Row, X);
            if (c != null && c.Occupant != null) AudioCues.Play(Cue.BuildingHere, pan);
            else if (c != null && c.Kind == CellKind.None) AudioCues.Play(Cue.CursorTick, pan, 0.7f);
            else AudioCues.Play(Cue.CursorTick, pan, c != null && c.Kind == CellKind.BackRoom ? 0.85f : 1f);
        }

        public static void FollowCamera()
        {
            if (!Plugin.Settings.CameraFollowsCursor.Value) return;
            CameraScript cam = CameraScript.Instance;
            if (cam == null || BuildingSystem.Instance == null) return;
            Vector3 target = WorldPosition;
            Vector3 p = cam.transform.position;
            cam.MoveCameraTo(new Vector3(target.x, p.y, p.z));
        }

        /// <summary>What is under the cursor, for movement announcements.</summary>
        public static string Describe(bool includeCustomers = true)
        {
            FloorCell c = Cell;
            if (c == null) return Loc.Get("nothing_here");
            var parts = new List<string>();
            if (c.Kind == CellKind.Special)
            {
                DeliveryObject d = Floor.Delivery;
                parts.Add(Loc.Get("delivery_truck"));
                parts.Add(d != null && ResourceManager.Instance.delivery.ItemList.Count > 0
                    ? Loc.Get("delivery_has_items", GameText.Contents(ResourceManager.Instance.delivery, 4))
                    : Loc.Get("delivery_empty"));
            }
            else
            {
                BuildingObject b = Building;
                if (b != null) parts.Add(GameText.BuildingSummary(b));
                else parts.Add(c.Kind == CellKind.ShopFloor ? Loc.Get("cell_shop_floor") : c.Kind == CellKind.BackRoom ? Loc.Get("cell_back_room") : Loc.Get("cell_wall"));
            }
            if (includeCustomers && c.Kind != CellKind.Special)
            {
                List<CustomerObject> customers = StoreFloor.CustomersAt(c.X);
                if (customers.Count == 1) parts.Add(Loc.Get("customer_here", GameText.CustomerName(customers[0])));
                else if (customers.Count > 1) parts.Add(Loc.Get("customers_here", customers.Count));
            }
            parts.Add(Loc.Get("cell_position", Row.Cells.IndexOf(c) + 1, Row.Cells.Count));
            return TextCleaner.Join(", ", parts.ToArray());
        }

        public static void Announce(bool interrupt = true) => SpeechService.Say(Describe(), interrupt);
    }
}
