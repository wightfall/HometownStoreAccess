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
    /// frame they differ, which raises the same event; that sync carries the current language. While a
    /// language chosen through the mod is shown (one the dropdown lacks, such as Japanese), it would switch
    /// the game to English or Thai for a moment, until the mod switched it back, so it is skipped then.
    /// </summary>
    [HarmonyPatch(typeof(OptionPanel), nameof(OptionPanel.OnLanguageDropdownValueChanged))]
    internal static class LanguageDropdownPatch
    {
        private static bool Prefix(int value)
        {
            if (value != (int)LanguageControl.CurrentLanguage)
            {
                GameLanguage.ClearOverride();
                return true;
            }
            return !GameLanguage.Overridden;
        }
    }

    /// <summary>
    /// The game's language dropdown offers English and Thai only. While a language chosen through the mod is
    /// shown (Japanese, say), the dropdown still shows one of the two, and picking that one changed no value,
    /// so the game did nothing. Such a pick now switches the game to it, as any other pick does.
    /// </summary>
    [HarmonyPatch(typeof(TMPro.TMP_Dropdown), "OnSelectItem")]
    internal static class LanguageDropdownSamePickPatch
    {
        private static void Prefix(TMPro.TMP_Dropdown __instance, out int __state) => __state = __instance != null ? __instance.value : -1;

        private static void Postfix(TMPro.TMP_Dropdown __instance, int __state)
        {
            if (__instance == null || __state < 0 || __instance.value != __state || __instance.GetComponent<LanguageDropdown>() == null) return;
            var picked = (Language)__state;
            if (!GameLanguage.Overridden || GameLanguage.Shows(picked)) return;
            GameLanguage.ClearOverride();
            LanguageControl lc = LanguageControl.Instance;
            if (lc != null) lc.SetLanguage(picked);
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
