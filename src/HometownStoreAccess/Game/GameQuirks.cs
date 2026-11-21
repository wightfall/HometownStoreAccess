namespace HometownStoreAccess.Game
{
    /// <summary>Game behaviour that gets in the way of keyboard and screen reader play.</summary>
    internal static class GameQuirks
    {
        private static bool _cheatChecked;
        private static bool? _uiHidden;

        public static void Update()
        {
            // F3 hides the game's whole on-screen interface (for screenshots); menus look empty then.
            bool hidden = HideUI.IsHidden;
            if (_uiHidden.HasValue && hidden != _uiHidden.Value)
            {
                Speech.SpeechService.Say(Core.Loc.Get(hidden ? "quirk_ui_hidden" : "quirk_ui_shown"), true);
            }
            _uiHidden = hidden;

            // GameControl exists only in the game: asked for anywhere else, the game searches the whole
            // scene for it (twice) on every call.
            AccessibilityRoot root = AccessibilityRoot.Instance;
            if (root == null || !root.InGame) return;
            GameControl gc = GameControl.Instance;
            if (gc == null) return;
            // F1 is the mod's help key. The game turns on hidden developer cheats (F2 money, F4 every
            // shop, F8, F9) when F1 is pressed three times within a second, which happens easily when
            // help is asked for again. Never let the presses add up.
            gc.f1PressCount = 0;
            if (!_cheatChecked && GlobeOption.Instance != null)
            {
                _cheatChecked = true;
                if (GlobeOption.Instance.devCheat && !Plugin.Settings.AllowDevCheats.Value)
                {
                    GlobeOption.Instance.devCheat = false;
                    Log.Info("Turned off the game's developer cheats (switched on by pressing F1 three times).");
                }
            }
        }
    }
}
