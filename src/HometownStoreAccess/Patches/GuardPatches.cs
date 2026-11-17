using HarmonyLib;
using HometownStoreAccess.Contexts;

namespace HometownStoreAccess.Patches
{
    /// <summary>Stops the game's own keyboard handling while the mod has consumed the key (help overlay, Escape).</summary>
    [HarmonyPatch(typeof(GameControl), "Update")]
    internal static class GameControlUpdateGuard
    {
        private static bool Prefix() => !InputGuard.BlockGame;
    }

    [HarmonyPatch(typeof(MainMenuControl), "Update")]
    internal static class MainMenuUpdateGuard
    {
        private static bool Prefix() => !InputGuard.BlockGame;
    }

    [HarmonyPatch(typeof(DialogPanel), "Update")]
    internal static class DialogUpdateGuard
    {
        private static bool Prefix() => !InputGuard.BlockGame;
    }

    [HarmonyPatch(typeof(CongratulationPanel), "Update")]
    internal static class CongratulationUpdateGuard
    {
        private static bool Prefix() => !InputGuard.BlockGame;
    }

    /// <summary>
    /// The player picked a language with the game's own dropdown: drop the mod's language override.
    /// The game's LanguageDropdown.Update also sets the dropdown to LanguageControl.CurrentLanguage every
    /// frame they differ, which raises the same event; that sync carries the current language and is ignored.
    /// </summary>
    [HarmonyPatch(typeof(OptionPanel), nameof(OptionPanel.OnLanguageDropdownValueChanged))]
    internal static class LanguageDropdownPatch
    {
        private static void Prefix(int value)
        {
            if (value != (int)LanguageControl.CurrentLanguage) GameLanguage.ClearOverride();
        }
    }
}

namespace HometownStoreAccess.Patches
{
    /// <summary>
    /// F1 is the mod's help key; the game toggles its developer cheats when F1 is pressed three times
    /// within a second. Reset that counter so asking for help never enables cheats.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(GameControl), "GetKeyShortcutInputCommand")]
    internal static class F1CheatGuard
    {
        private static void Prefix(GameControl __instance) => __instance.f1PressCount = 0;
    }
}
