using System;
using DG.Tweening;
using HarmonyLib;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;
using UnityEngine;

namespace HometownStoreAccess.Patches
{
    /// <summary>
    /// When the game starts, the main menu plays a logo intro of about 15 seconds before its buttons
    /// appear; any key skips it. With the SkipIntro setting the mod skips it at once, the way the game
    /// does when a key is pressed, so the main menu can be used straight after the mod's welcome.
    /// </summary>
    [HarmonyPatch(typeof(TransitionInMainMenu), "Start")]
    internal static class IntroSkipPatch
    {
        private static void Postfix(TransitionInMainMenu __instance)
        {
            if (__instance == null || __instance.transitionIsDone) return;
            if (Plugin.Settings == null || !Plugin.Settings.SkipIntro.Value) return;
            try
            {
                __instance.SkipTransition();
                Log.Info("Logo intro skipped (setting SkipIntro)");
            }
            catch (Exception e)
            {
                Log.Warn("Could not skip the logo intro: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Continue, New game and loading a save first fade the screen to black for two seconds, in
    /// silence. Say at once that the game is loading; with SkipIntro the fade is also made short.
    /// </summary>
    [HarmonyPatch(typeof(TransitionInMainMenu), nameof(TransitionInMainMenu.RunLoadingScreen))]
    internal static class LoadingFadePatch
    {
        /// <summary>When "Loading" was last said (so the scene load does not say it again).</summary>
        public static float AnnouncedAt { get; private set; } = -100f;

        private static bool Prefix(TransitionInMainMenu __instance, Action onComplete)
        {
            SpeechService.Say(Loc.Get("loading_game"), true);
            AnnouncedAt = Time.unscaledTime;
            if (__instance == null || Plugin.Settings == null || !Plugin.Settings.SkipIntro.Value) return true;
            try
            {
                CanvasGroup black = __instance.blackoutImage;
                black.DOKill();
                __instance.transitionSequence?.Kill();
                __instance.transitionSequence = DOTween.Sequence();
                __instance.transitionSequence.Append(DOTween.To(() => black.alpha, a => black.alpha = a, 1f, 0.25f).SetTarget(black)).OnComplete(delegate
                {
                    onComplete?.Invoke();
                });
                return false;
            }
            catch (Exception e)
            {
                Log.Warn("Could not shorten the loading fade: " + e.Message);
                return true;
            }
        }
    }

    /// <summary>Going back to the main menu from the game takes a few silent seconds: say so.</summary>
    [HarmonyPatch(typeof(GlobeOption), nameof(GlobeOption.ReturnToMenu))]
    internal static class ReturnToMenuPatch
    {
        private static void Postfix()
        {
            SpeechService.Say(Loc.Get("loading_menu"), true);
        }
    }
}
