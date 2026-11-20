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
            // Renaming onto an existing name makes the game move the file twice; the second move fails
            // after the file already has its unique name. Nothing is lost, so do not pass the error on.
            bool io = __exception is System.IO.IOException;
            if (io) Log.Debug("Save rename: " + __exception.Message);
            if (_depth == 0 && __instance != null && _oldName != __instance._currentSaveFileName) Rebuild(__instance);
            return io ? null : __exception;
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
}
