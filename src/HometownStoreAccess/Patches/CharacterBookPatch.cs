using HarmonyLib;
using HometownStoreAccess.Core;
using UnityEngine;

namespace HometownStoreAccess.Patches
{
    /// <summary>
    /// Game bug in the demo: the character book's entries have no pictures, so the game's own check (by picture)
    /// never marks anyone as met, and the book stays "???" for everyone. An entry without a picture is matched by
    /// name instead ("Portrait_Kid" is "CharacterPortrait_Kid") when a conversation shows that character, as the
    /// game meant. The book and the save then keep it, as they do for the game's own marks.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCollectionManager), nameof(CharacterCollectionManager.MarkMet))]
    internal static class CharacterBookPatch
    {
        private static void Postfix(CharacterCollectionManager __instance, Sprite portraitSprite)
        {
            if (__instance == null || portraitSprite == null || __instance.allCharacterPortraits == null) return;
            string key = PortraitFaces.CharacterKey(portraitSprite.name);
            if (key.Length == 0) return;
            foreach (CharacterPortrait cp in __instance.allCharacterPortraits)
            {
                if (cp == null || cp.portrait != null || cp.isSecret) continue;
                if (PortraitFaces.CharacterKey(cp.name) != key) continue;
                if (__instance.metCharacters.Add(cp)) Log.Debug("Character book: met " + cp.name);
                return;
            }
        }
    }
}
