using System;
using HarmonyLib;
using HometownStoreAccess.Core;
using HometownStoreAccess.Speech;
using Steamworks.Data;

namespace HometownStoreAccess.Patches
{
    /// <summary>
    /// Testing: while the test commands are on (setting TestHooks), the game's achievement unlocks are not sent to
    /// Steam. A test conversation, event or reward can trigger one, and a test must never unlock a player's real
    /// achievements. The unlock is written to the log instead.
    /// </summary>
    [HarmonyPatch(typeof(SteamIntegration), nameof(SteamIntegration.UnlockAchievement))]
    [HarmonyPriority(Priority.First)]
    internal static class TestAchievementGuard
    {
        // The game's money check asks every frame: each achievement is logged once.
        private static readonly System.Collections.Generic.HashSet<E_Achievement> Logged = new System.Collections.Generic.HashSet<E_Achievement>();

        private static bool Prefix(E_Achievement achievement)
        {
            if (Plugin.Settings == null || !Plugin.Settings.TestHooks.Value) return true;
            if (Logged.Add(achievement)) Log.Info("[test] achievement " + achievement + " not unlocked: the test commands are on");
            return false;
        }
    }

    /// <summary>
    /// Steam shows an unlocked achievement only in its overlay, which screen readers do not read.
    /// When the game unlocks one that was still locked, say its Steam name and description.
    /// </summary>
    [HarmonyPatch(typeof(SteamIntegration), nameof(SteamIntegration.UnlockAchievement))]
    internal static class AchievementPatch
    {
        private static void Prefix(SteamIntegration __instance, E_Achievement achievement, out bool __state)
        {
            __state = true;
            try
            {
                if (__instance != null && __instance.connectedToSteam) __state = __instance.IsAchievementUnlocked(achievement);
            }
            catch
            {
                __state = true;
            }
        }

        private static void Postfix(SteamIntegration __instance, E_Achievement achievement, bool __state)
        {
            if (__state || __instance == null || !__instance.connectedToSteam) return;
            try
            {
                if (!__instance.IsAchievementUnlocked(achievement)) return;
                if (Plugin.Settings != null && !Plugin.Settings.AnnounceAchievements.Value) return;
                SpeechService.Say(Describe(achievement), false, MsgCategory.Events);
            }
            catch (Exception e)
            {
                Log.Debug("Achievement announcement failed: " + e.Message);
            }
        }

        /// <summary>"Achievement unlocked: name. description", with Steam's own texts (in Steam's language).</summary>
        public static string Describe(E_Achievement a)
        {
            string name = null, description = null;
            try
            {
                var steam = new Achievement("Achievement_" + (int)a);
                name = TextCleaner.Clean(steam.Name);
                description = TextCleaner.Clean(steam.Description);
            }
            catch
            {
                // Steam is not running: fall back to the game's own name for it.
            }
            if (string.IsNullOrEmpty(name)) name = TextCleaner.HumanizeName(a.ToString());
            return TextCleaner.Join(". ", Loc.Get("achievement_unlocked", name), description);
        }
    }
}
