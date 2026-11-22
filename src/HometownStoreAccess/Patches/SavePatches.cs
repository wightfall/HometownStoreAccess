using System.Collections.Generic;
using HarmonyLib;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;

namespace HometownStoreAccess.Patches
{
    /// <summary>The game saves without a word: say that it worked and under which name.</summary>
    [HarmonyPatch(typeof(SaveSlotPanel), nameof(SaveSlotPanel.SaveAndRefresh))]
    internal static class SaveDonePatch
    {
        private static void Postfix(string saveName)
        {
            SpeechService.Say(Loc.Get("save_done", saveName), false, MsgCategory.Notices);
        }
    }

    /// <summary>
    /// Renaming a save renames the file, but the slot's Load and Delete buttons keep the old name, so
    /// afterwards they act on a file that no longer exists. Rebuild the list so they use the new name.
    /// </summary>
    [HarmonyPatch(typeof(ContentSaveSlot), nameof(ContentSaveSlot.RenameSave))]
    internal static class SaveRenamePatch
    {
        private static int _depth;
        private static string _oldName;

        private static void Prefix(ContentSaveSlot __instance)
        {
            // The game calls itself again to find a unique name: remember the name of the outer call.
            if (_depth == 0) _oldName = __instance._currentSaveFileName;
            _depth++;
        }

        private static System.Exception Finalizer(ContentSaveSlot __instance, System.Exception __exception)
        {
            _depth--;
            // An inner call (the game looking for a unique name): the outer one decides.
            if (_depth > 0) return __exception;
            bool renamed = __instance != null && _oldName != __instance._currentSaveFileName;
            if (renamed) Rebuild(__instance);
            // Renaming onto an existing name makes the game move the file twice; the second move fails after
            // the file already has its unique name. Nothing is lost, so that error is not passed on. Any other
            // failure (the file locked by cloud sync, say) goes on to the game as before, and to the log.
            if (__exception is System.IO.IOException && renamed)
            {
                Log.Debug("Save rename: " + __exception.Message);
                return null;
            }
            if (__exception != null) Log.Warn("Save rename failed: " + __exception.Message);
            return __exception;
        }

        private static void Rebuild(ContentSaveSlot slot)
        {
            SaveSlotPanel panel = slot._saveSlotPanel;
            if (panel == null) return;
            bool saving = panel.warningBetweenPhase != null && panel.warningBetweenPhase.activeSelf;
            if (saving) panel.ShowSaveGame();
            else panel.ShowLoadGame();
            SpeechService.Say(Loc.Get("save_renamed", slot._currentSaveFileName), false, MsgCategory.Notices);
        }
    }

    /// <summary>
    /// Game bug: the "this will overwrite this save" warning stayed armed after Cancel (or after the save
    /// screen was closed), so the next Confirm with any taken name overwrote that save at once, without
    /// a warning. Every new name box now starts without it, and changing the name clears it, since the
    /// warning was about the earlier name. The game then warns again before overwriting.
    /// </summary>
    [HarmonyPatch(typeof(SaveSlotPanel), "OpenNewSaveInputPanel")]
    internal static class SaveOverwriteWarningPatch
    {
        private static readonly HashSet<int> Hooked = new HashSet<int>();

        private static void Postfix(SaveSlotPanel __instance)
        {
            if (__instance == null) return;
            if (__instance.pendingConfirm != null) __instance.pendingConfirm.SetActive(false);
            if (__instance.inputNameField == null || !Hooked.Add(__instance.GetInstanceID())) return;
            SaveSlotPanel panel = __instance;
            panel.inputNameField.onValueChanged.AddListener(_ =>
            {
                if (panel != null && panel.pendingConfirm != null && panel.pendingConfirm.activeSelf) panel.pendingConfirm.SetActive(false);
            });
        }
    }
}
