using HarmonyLib;
using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Input;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess.Game
{
    /// <summary>
    /// Keyboard placement of buildings. While active, the "ghost" building follows the floor cursor
    /// instead of the mouse. Moving the mouse hands control back to the mouse for sighted players.
    /// </summary>
    internal static class Placement
    {
        private static Vector3 _lastMouse;

        /// <summary>True when the keyboard, not the mouse, positions the ghost building.</summary>
        public static bool KeyboardControl { get; private set; } = true;

        public static BuildingObject Temp => BuildingSystem.Instance != null ? BuildingSystem.Instance.currentTempBuilding : null;
        public static bool Active => Temp != null;

        public static void TakeKeyboardControl()
        {
            KeyboardControl = true;
            _lastMouse = UnityEngine.Input.mousePosition;
        }

        /// <summary>Called every frame: a real mouse movement gives control back to the mouse.</summary>
        public static void Update()
        {
            Vector3 m = UnityEngine.Input.mousePosition;
            if (KeyboardControl && (m - _lastMouse).sqrMagnitude > 400f) KeyboardControl = false;
            _lastMouse = m;
        }

        /// <summary>Positions a freshly created ghost at the nearest spot where it fits, and announces it.</summary>
        public static void Begin(BuildingObject temp)
        {
            TakeKeyboardControl();
            FloorCursor.Refresh();
            CellKind need = StoreFloor.RequiredKind(temp.ID);
            int size = Mathf.Max(1, temp.ID.sizeX);
            FloorRow row = FloorCursor.Row;
            int? fit = FloorNav.FirstFit(row, size, need, FloorCursor.X);
            if (!fit.HasValue)
            {
                foreach (FloorRow r in FloorCursor.Floor.Rows)
                {
                    fit = FloorNav.FirstFit(r, size, need, r.MinX);
                    if (fit.HasValue) { FloorCursor.SetPosition(fit.Value, r.Y); break; }
                }
            }
            else
            {
                FloorCursor.SetPosition(fit.Value, row.Y);
            }
            Follow();
            string intro = Loc.Get("placing_start", GameText.Name(temp.ID), size);
            SpeechService.Say(TextCleaner.Join(". ", intro, DescribeSpot()), true);
        }

        public static void Follow()
        {
            BuildingObject temp = Temp;
            if (temp == null) return;
            BuildingSystem.Instance.FollowBuilding(temp, FloorCursor.WorldPosition);
        }

        /// <summary>"Cell 3 of 11: can place" / "blocked by Wooden storage" / "needs the back room".</summary>
        public static string DescribeSpot()
        {
            BuildingObject temp = Temp;
            if (temp == null) return FloorCursor.Describe();
            FloorRow row = FloorCursor.Row;
            FloorCell cell = FloorCursor.Cell;
            int size = Mathf.Max(1, temp.ID.sizeX);
            PlacementCheck check = FloorNav.CheckPlacement(row, FloorCursor.X, size, StoreFloor.RequiredKind(temp.ID));
            bool gameSays = temp.CanBePlaced(FloorCursor.WorldPosition);
            string where = Loc.Get("cell_position", row.Cells.IndexOf(cell) + 1, row.Cells.Count);
            string status;
            if (gameSays) status = Loc.Get("place_ok");
            else
            {
                switch (check.Problem)
                {
                    case PlacementProblem.Occupied:
                        FloorCursor.Floor.Buildings.TryGetValue(check.BlockingOccupant ?? string.Empty, out BuildingObject blocker);
                        status = Loc.Get("place_blocked_by", blocker != null ? GameText.Name(blocker.ID) : "?");
                        break;
                    case PlacementProblem.WrongArea:
                        status = StoreFloor.RequiredKind(temp.ID) == CellKind.BackRoom ? Loc.Get("place_needs_back_room") : Loc.Get("place_needs_shop_floor");
                        break;
                    default:
                        status = Loc.Get("place_outside");
                        break;
                }
            }
            return TextCleaner.Join(", ", where, status);
        }

        public static void Moved()
        {
            TakeKeyboardControl();
            Follow();
            BuildingObject temp = Temp;
            bool ok = temp != null && temp.CanBePlaced(FloorCursor.WorldPosition);
            AudioCues.Play(ok ? Cue.CursorTick : Cue.PlaceBlocked, FloorNav.Pan(FloorCursor.Row, FloorCursor.X), ok ? 1.3f : 1f, ok ? 1f : 0.6f);
            SpeechService.Say(DescribeSpot(), true);
        }

        /// <summary>Places the ghost at the cursor if the game allows it.</summary>
        public static bool TryPlace()
        {
            BuildingObject temp = Temp;
            if (temp == null) return false;
            TakeKeyboardControl();
            Vector3 pos = FloorCursor.WorldPosition;
            BuildingSystem.Instance.FollowBuilding(temp, pos);
            if (!temp.CanBePlaced(pos))
            {
                AudioCues.Play(Cue.Error);
                Controller.Rumble(0.4f, 0.15f);
                SpeechService.Say(Loc.Get("place_failed", DescribeSpot()), true);
                return false;
            }
            string name = GameText.Name(temp.ID);
            temp.Place(pos);
            BuildingSystem.Instance.currentTempBuilding = null;
            BuildingSystem.Instance.Refresh();
            AudioCues.Play(Cue.PlaceOk, FloorNav.Pan(FloorCursor.Row, FloorCursor.X));
            SpeechService.Say(Loc.Get("placed", name), true);
            return true;
        }
    }

    /// <summary>While the keyboard controls placement, the game's per-frame "follow the mouse" uses the cursor instead.</summary>
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.FollowBuilding))]
    internal static class FollowBuildingPatch
    {
        private static void Prefix(BuildingObject building, ref Vector2 mousePosition2D)
        {
            if (!Placement.KeyboardControl || building == null) return;
            if (BuildingSystem.Instance == null || building != BuildingSystem.Instance.currentTempBuilding) return;
            GameControl gc = GameControl.Instance;
            if (gc == null || gc.CurrentGameplayMode == GameplayMode.Default) return;
            if (BuildingSystem.Instance.AreaTilemap == null) return;
            // Keep explicit restore positions (Escape in move mode) untouched.
            if (gc.CurrentGameplayMode == GameplayMode.MoveMode && (Vector2)gc.beforeMovePosition == mousePosition2D) return;
            mousePosition2D = FloorCursor.WorldPosition;
        }
    }

    /// <summary>When the camera follows the keyboard cursor, the arrow keys must not also scroll it.</summary>
    [HarmonyPatch(typeof(CameraScript), "Update")]
    internal static class CameraUpdatePatch
    {
        private static bool Prefix(CameraScript __instance)
        {
            if (Plugin.Settings == null || !Plugin.Settings.CameraFollowsCursor.Value) return true;
            __instance.SetCameraBounds();
            return false;
        }
    }

    /// <summary>The first tutorial step waits for the camera to move; moving the floor cursor counts.</summary>
    [HarmonyPatch(typeof(CameraScript), nameof(CameraScript.IsMove), MethodType.Getter)]
    internal static class CameraIsMovePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (FloorCursor.MovedRecently) __result = true;
        }
    }
}
