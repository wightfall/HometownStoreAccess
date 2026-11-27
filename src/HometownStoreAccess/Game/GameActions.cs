using HometownStoreAccess.Audio;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;
using HometownStoreAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Game
{
    /// <summary>
    /// Game operations triggered from the keyboard. Each one mirrors what the game does for the
    /// equivalent mouse action, including its preconditions and confirmation popups.
    /// </summary>
    internal static class GameActions
    {
        public static bool PopupOpen =>
            UIPanelManager.Instance != null && (UIPanelManager.Instance.CheckIfConfirmActive() || UIPanelManager.Instance.CheckIfNotifyActive());

        /// <summary>Opens the panel of a building, like double-clicking it.</summary>
        public static void OpenBuilding(BuildingObject b)
        {
            if (b == null) return;
            if (GameControl.Instance.CurrentGameplayMode != GameplayMode.Default)
            {
                SpeechService.Say(Loc.Get("cannot_open_in_build_mode"), true);
                return;
            }
            var col = b.GetComponent<BoxCollider2D>();
            if (!b.IsBuild || col == null || !col.enabled)
            {
                SpeechService.Say(Loc.Get("cannot_open_yet"), true);
                AudioCues.Play(Cue.Error);
                return;
            }
            if (b is BuildingObject && !(b is ShelfObject) && !(b is StorageObject) && !(b is ManufactureObject) && !(b is CashierObject) && !(b is DeliveryObject))
            {
                SpeechService.Say(Loc.Get("nothing_to_open"), true);
                return;
            }
            AudioCues.Play(Cue.Open);
            UIPanelManager.Instance.OpenPanel(b);
        }

        public static void OpenDelivery(DeliveryObject d)
        {
            if (d == null || !StoreFloor.IsDeliveryAvailable(d))
            {
                SpeechService.Say(Loc.Get("delivery_not_here"), true);
                return;
            }
            if (GameControl.Instance.CurrentGameplayMode != GameplayMode.Default)
            {
                SpeechService.Say(Loc.Get("cannot_open_in_build_mode"), true);
                return;
            }
            AudioCues.Play(Cue.Open);
            UIPanelManager.Instance.OpenPanel(d);
        }

        /// <summary>Starts placing a blueprint (as clicking it in the build panel does).</summary>
        public static bool StartPlacing(Building building)
        {
            if (building == null || PopupOpen) return false;
            GameControl.Instance.ChangeMode(GameplayMode.BuildMode);
            BuildingSystem.Instance.CreateBuilding(building);
            BuildingObject temp = BuildingSystem.Instance.currentTempBuilding;
            if (temp == null) return false;
            Placement.Begin(temp);
            return true;
        }

        /// <summary>Lifts a placed building so it can be moved (move mode).</summary>
        public static bool StartMoving(BuildingObject b)
        {
            if (b == null || b is DeliveryObject || PopupOpen) return false;
            GameControl gc = GameControl.Instance;
            if (gc.CurrentGameplayMode != GameplayMode.MoveMode) gc.ChangeMode(GameplayMode.MoveMode);
            Vector3 offset = new Vector3(b.ID.sizeX - 1, 0f, 0f);
            gc.beforeMovePosition = b.transform.position - offset;
            BuildingSystem.Instance.MoveBuilding(b);
            if (BuildingSystem.Instance.currentTempBuilding == null) return false;
            Placement.Begin(BuildingSystem.Instance.currentTempBuilding);
            return true;
        }

        /// <summary>Puts a placed building back into the blueprints (pickup mode), asking first if it holds items.</summary>
        public static void PickUp(BuildingObject building)
        {
            if (building == null || building is DeliveryObject || PopupOpen) return;
            int count = 0;
            // Slots locked to a kind of goods stay when empty: the game counts only the filled ones (1.7.12).
            if (building is ShelfObject shelf) count += shelf.ShelfInventory.FilledSlotCount();
            else if (building is ManufactureObject m) count += m.InputInventory.ItemList.Count + m.OutputInventory.ItemList.Count;
            string name = GameText.Name(building.ID);
            if (count > 0)
            {
                UIPanelManager.Instance.ShowConfirmPanel(LocalizationManager.GetString(LocalizationTable.ConfirmAndNotify, "UI/Popup/GameControl/A"), delegate
                {
                    BuildingSystem.Instance.PickupBuilding(building, force: true);
                    SpeechService.Say(Loc.Get("picked_up", name), true);
                });
            }
            else
            {
                BuildingSystem.Instance.PickupBuilding(building);
                SpeechService.Say(Loc.Get("picked_up", name), true);
            }
        }

        /// <summary>Sells one blueprint for 20% of its value (same rule and confirmation as the build panel's sell button).</summary>
        public static void SellBlueprint(Building building)
        {
            if (building == null || building is Cashier || PopupOpen) return;
            if (!GameControl.Instance.IsPlayedTutorial)
            {
                UIPanelManager.Instance.ShowNotifyPanel("Selling is disabled during the tutorial.");
                return;
            }
            if (!BuildingSystem.Instance.HasEnoughBlueprint(building, 1)) return;
            int price = Mathf.RoundToInt(building.marketValue * 0.2f);
            string name = GameText.Name(building);
            UIPanelManager.Instance.ShowConfirmPanel(LocalizationManager.GetString(LocalizationTable.ConfirmAndNotify, "UI/Popup/ContentBuilding", name, price.ToString()), delegate
            {
                if (SummaryPanel.Instance != null) SummaryPanel.Instance.AddBill(isSell: true, building, 1, price);
                EventAnnouncer.QuietCash(a => a == price);
                ResourceManager.Instance.ModifyCash(price);
                EventAnnouncer.ClearCashQuiet();
                BuildingSystem.Instance.RemoveBlueprint(building, 1);
                SpeechService.Say(Plugin.Settings.AnnounceCashChanges.Value
                    ? Loc.Get("sold_blueprint_cash", name, price, ResourceManager.Instance.cash)
                    : Loc.Get("sold_blueprint", name, price), true);
            });
        }

        /// <summary>Clicks a HUD button only if a sighted player could click it right now.</summary>
        public static bool ClickIfAvailable(Selectable sel, string unavailableMessage = null)
        {
            if (sel == null || !UiUtil.IsShown(sel.gameObject))
            {
                SpeechService.Say(unavailableMessage ?? Loc.Get("not_available_now"), true);
                AudioCues.Play(Cue.Error);
                return false;
            }
            if (!sel.IsInteractable())
            {
                SpeechService.Say(unavailableMessage ?? Loc.Get("not_available_now"), true);
                AudioCues.Play(Cue.Error);
                return false;
            }
            UiUtil.Click(sel.gameObject);
            return true;
        }

        /// <summary>Same as the Escape key in the game: closes the topmost panel or opens the pause menu.</summary>
        public static void Back()
        {
            // A closed sub-list leaves the same screen: announce the new focus once the list is rebuilt.
            AccessibilityRoot.Instance?.Contexts?.AnnounceFocusIfChanged();
            if (Contexts.Overlay.IsOpen)
            {
                Contexts.Overlay.Close();
                return;
            }
            if (Contexts.StorageSummaryContext.CloseOpen()) return;
            // Only in the game scene: outside it the game's getter searches the whole scene, twice.
            AccessibilityRoot root = AccessibilityRoot.Instance;
            UIPanelManager ui = root != null && root.InGame ? UIPanelManager.Instance : null;
            if (ui == null)
            {
                MainMenuControl mm = UI.SceneCache.Get<MainMenuControl>();
                if (mm == null) return;
                if (mm.CheckIfConfirmActive()) mm.CancelConfirmation();
                else if (mm.IsHavePanelToClose()) mm.CloseLastPanel();
                else GlobeOption.Instance.ToggleOptionPanel();
                return;
            }
            GameControl gc = GameControl.Instance;
            if (GlobeOption.Instance.optionPanel != null && GlobeOption.Instance.GetOptionPanelActiveSelf())
            {
                if (ui.CheckIfNotifyActive()) ui.CancelNotify();
                else if (ui.CheckIfConfirmActive()) ui.CancelConfirmation();
                else if (ui.CheckIfCongratulationActive()) ui.CancelCongratulation();
                else GlobeOption.Instance.ToggleOptionPanel();
                return;
            }
            if (gc.CurrentGameplayMode == GameplayMode.Default)
            {
                if (gc.HandleCancelingESC()) return;
                GlobeOption.Instance.ToggleOptionPanel();
                return;
            }
            if (gc.HandleCancelingESC()) return;
            BuildingSystem bs = BuildingSystem.Instance;
            switch (gc.CurrentGameplayMode)
            {
                case GameplayMode.BuildMode:
                    if (bs.currentTempBuilding != null)
                    {
                        bs.AddBlueprint(bs.currentTempBuilding.ID);
                        Object.Destroy(bs.currentTempBuilding.gameObject);
                        bs.currentTempBuilding = null;
                        bs.Refresh();
                    }
                    else
                    {
                        ui.EnableBuildingPanel(enable: false);
                    }
                    break;
                case GameplayMode.MoveMode:
                    if (bs.currentTempBuilding != null)
                    {
                        BuildingObject temp = bs.currentTempBuilding;
                        bs.FollowBuilding(temp, gc.beforeMovePosition);
                        if (temp.CanBePlaced(gc.beforeMovePosition))
                        {
                            temp.Place(gc.beforeMovePosition);
                            bs.currentTempBuilding = null;
                            bs.Refresh();
                        }
                    }
                    else
                    {
                        gc.ChangeMode(GameplayMode.BuildMode);
                    }
                    break;
                case GameplayMode.PickupMode:
                    gc.ChangeMode(GameplayMode.BuildMode);
                    break;
            }
        }
    }
}
