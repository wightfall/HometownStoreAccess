using System.Collections.Generic;
using HometownStoreAccess.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HometownStoreAccess.Game
{
    /// <summary>Snapshot of the shop floor (rows of cells, buildings, delivery truck) built from the game's tilemaps.</summary>
    internal sealed class StoreFloor
    {
        public const string DeliveryKey = "delivery";

        public readonly List<FloorRow> Rows = new List<FloorRow>();
        public readonly Dictionary<string, BuildingObject> Buildings = new Dictionary<string, BuildingObject>();
        public DeliveryObject Delivery;

        public static string KeyOf(BuildingObject b) => b == null ? null : "b" + b.GetInstanceID();

        public static StoreFloor Capture()
        {
            var floor = new StoreFloor();
            BuildingSystem bs = BuildingSystem.Instance;
            if (bs == null || bs.AreaTilemap == null) return floor;
            Tilemap area = bs.AreaTilemap;
            BoundsInt bounds = area.cellBounds;

            var occupancy = new Dictionary<Vector2Int, string>();
            foreach (BuildingObject b in bs.allBuildings)
            {
                if (b == null) continue;
                string key = KeyOf(b);
                floor.Buildings[key] = b;
                BoundsInt a = b.AreaTemp;
                for (int x = a.xMin; x < a.xMax; x++)
                {
                    for (int y = a.yMin; y < a.yMax; y++) occupancy[new Vector2Int(x, y)] = key;
                }
            }

            for (int y = bounds.yMax - 1; y >= bounds.yMin; y--)
            {
                int minX = int.MaxValue, maxX = int.MinValue;
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    if (area.GetTile(new Vector3Int(x, y, 0)) != null)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
                }
                if (minX > maxX) continue;
                var row = new FloorRow(y);
                for (int x = minX; x <= maxX; x++)
                {
                    CellKind kind = KindOf(area.GetTile(new Vector3Int(x, y, 0)));
                    occupancy.TryGetValue(new Vector2Int(x, y), out string occ);
                    row.Cells.Add(new FloorCell(x, kind, occ));
                }
                floor.Rows.Add(row);
            }

            DeliveryObject d = UI.SceneCache.Get<DeliveryObject>();
            if (d != null && IsDeliveryAvailable(d) && floor.Rows.Count > 0)
            {
                floor.Delivery = d;
                FloorRow bottom = floor.Rows[floor.Rows.Count - 1];
                bottom.Cells.Insert(0, new FloorCell(bottom.MinX - 1, CellKind.Special, DeliveryKey));
            }
            return floor;
        }

        /// <summary>The truck can be opened only while it is visible and its collider is enabled, as for a mouse click.</summary>
        public static bool IsDeliveryAvailable(DeliveryObject d)
        {
            if (d == null || !d.gameObject.activeInHierarchy) return false;
            var sr = d.GetComponent<SpriteRenderer>();
            var col = d.GetComponent<BoxCollider2D>();
            return sr != null && sr.color.a > 0.5f && col != null && col.enabled;
        }

        public static CellKind KindOf(TileBase tile)
        {
            if (tile == null) return CellKind.None;
            foreach (KeyValuePair<TileType, TileBase> kv in BuildingSystem.tileBases)
            {
                if (kv.Value != tile) continue;
                switch (kv.Key)
                {
                    case TileType.White: return CellKind.ShopFloor;
                    case TileType.Yellow: return CellKind.BackRoom;
                    default: return CellKind.None;
                }
            }
            string n = tile.name.ToLowerInvariant();
            if (n.Contains("white")) return CellKind.ShopFloor;
            if (n.Contains("yellow")) return CellKind.BackRoom;
            return CellKind.None;
        }

        /// <summary>The kind of floor a building needs (mirrors BuildingSystem.TileTypeBuilding).</summary>
        public static CellKind RequiredKind(Building b) => b is Storage || b is Manufacture ? CellKind.BackRoom : CellKind.ShopFloor;

        public FloorRow RowAt(int y)
        {
            foreach (FloorRow r in Rows)
            {
                if (r.Y == y) return r;
            }
            return null;
        }

        public BuildingObject BuildingAt(FloorRow row, int x)
        {
            FloorCell c = row?.At(x);
            if (c?.Occupant == null || c.Occupant == DeliveryKey) return null;
            Buildings.TryGetValue(c.Occupant, out BuildingObject b);
            return b;
        }

        public static Vector3 CellCenter(int x, int y)
        {
            BuildingSystem bs = BuildingSystem.Instance;
            return bs.AreaTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
        }

        public static int CellXOf(Vector3 world)
        {
            BuildingSystem bs = BuildingSystem.Instance;
            return bs.AreaTilemap.WorldToCell(world).x;
        }

        /// <summary>Customers currently standing in the given cell column.</summary>
        public static List<CustomerObject> CustomersAt(int x)
        {
            var list = new List<CustomerObject>();
            CustomerSpawnSystem css = CustomerSpawnSystem.Instance;
            if (css == null) return list;
            foreach (CustomerObject c in css.customerOnScreen)
            {
                if (c != null && CellXOf(c.transform.position) == x) list.Add(c);
            }
            return list;
        }
    }
}
