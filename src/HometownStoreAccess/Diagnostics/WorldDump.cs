using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HometownStoreAccess.Diagnostics
{
    /// <summary>Developer tool: writes the shop grid, buildings and customers to a text file.</summary>
    internal static class WorldDump
    {
        public static string Write()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== World dump " + DateTime.Now.ToString("s") + " ===");
            BuildingSystem bs = BuildingSystem.Instance;
            if (bs == null)
            {
                sb.AppendLine("No BuildingSystem in scene.");
            }
            else
            {
                DumpTilemap(sb, "Area", bs.AreaTilemap);
                DumpTilemap(sb, "Building", bs.BuildingTilemap);
                sb.AppendLine("Grid cell size: " + bs.gridLayout.cellSize);
                (float minX, float maxX) = bs.ShopAreaPositionX();
                sb.AppendLine("Shop area X: " + minX + " .. " + maxX);
                foreach (BuildingObject b in bs.allBuildings)
                {
                    sb.AppendLine("Building " + b.ID.name + " type=" + b.GetType().Name + " size=" + b.ID.sizeX + " pos=" + b.transform.position + " area=" + b.AreaTemp);
                }
                foreach (BuildingBlueprint bp in bs.currentBlueprint)
                {
                    sb.AppendLine("Blueprint " + bp.Id.name + " x" + bp.amount);
                }
            }
            if (CustomerSpawnSystem.Instance != null)
            {
                foreach (CustomerObject c in CustomerSpawnSystem.Instance.customerOnScreen)
                {
                    if (c == null) continue;
                    sb.AppendLine("Customer " + c.customerProfile?.customerID?.name + " state=" + c.currentCustomerState + " pos=" + c.transform.position + " target=" + c.targetPosition);
                }
                foreach (Transform sp in CustomerSpawnSystem.Instance.customerSpawnPoints)
                {
                    sb.AppendLine("Spawn point " + sp.name + " " + sp.position);
                }
            }
            if (Camera.main != null)
            {
                sb.AppendLine("Camera " + Camera.main.transform.position + " ortho=" + Camera.main.orthographicSize + " aspect=" + Camera.main.aspect);
            }
            string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "hsa_dumps");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "world_" + DateTime.Now.ToString("HHmmss") + ".txt");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            Log.Info("World dump written to " + file);
            return file;
        }

        private static void DumpTilemap(StringBuilder sb, string label, Tilemap map)
        {
            if (map == null)
            {
                sb.AppendLine(label + " tilemap: null");
                return;
            }
            BoundsInt bounds = map.cellBounds;
            sb.AppendLine(label + " tilemap '" + map.name + "' bounds=" + bounds + " active=" + map.gameObject.activeInHierarchy);
            for (int y = bounds.yMax - 1; y >= bounds.yMin; y--)
            {
                var row = new StringBuilder();
                bool any = false;
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    TileBase t = map.GetTile(new Vector3Int(x, y, 0));
                    if (t == null) { row.Append('.'); continue; }
                    any = true;
                    string n = t.name.ToLowerInvariant();
                    row.Append(n.Length > 0 ? n[0] : '?');
                }
                if (any) sb.AppendLine("  y=" + y + " x0=" + bounds.xMin + " " + row);
            }
            Vector3Int c0 = new Vector3Int(bounds.xMin, bounds.yMin, 0);
            sb.AppendLine("  cell(" + c0.x + "," + c0.y + ") center world=" + map.GetCellCenterWorld(c0));
        }
    }
}
