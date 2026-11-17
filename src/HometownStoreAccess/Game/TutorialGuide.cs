using HometownStoreAccess.Core;

namespace HometownStoreAccess.Game
{
    /// <summary>
    /// The game's tutorial tells you where to click. This adds, for every step, which mod keys do the
    /// same thing. The step is derived from the tutorial manager's completion flags.
    /// </summary>
    internal static class TutorialGuide
    {
        /// <summary>Localization key of the hint for the current step, or null when there is none.</summary>
        public static string CurrentKey()
        {
            TutorialManager t = TutorialManager.Instance;
            GameControl gc = GameControl.Instance;
            if (t == null || gc == null || gc.IsPlayedTutorial) return null;
            if (!t.IsTransitioningComplete) return null;
            if (!t.IsTutorial0Complete) return "tg_move";
            if (!t.IsTutorial0halfComplete) return "tg_build";
            if (!t.IsTutorial1Complete) return "tg_place";
            if (!t.IsTutorial2Complete) return "tg_open_shelf";
            if (!t.IsTutorial3Complete || !t.IsTutorial3halfComplete) return null;
            if (!t.IsTutorial3finalComplete) return "tg_stock";
            if (!t.IsTutorial4Complete) return null;
            if (!t.IsTutorial4halfComplete) return "tg_open";
            if (!t.IsTutorial5Complete || !t.IsTutorial5halfComplete) return null;
            if (!t.IsTutorial6Complete) return "tg_wait";
            if (!t.IsTutorial7Complete) return "tg_map";
            if (!t.IsTutorial8Complete) return "tg_travel";
            if (!t.IsTutorial9Complete) return null;
            if (!t.IsTutorial10Complete) return "tg_travel";
            if (!t.IsTutorial11Complete) return "tg_buy";
            if (!t.IsTutorial12Complete) return "tg_end_day";
            if (!t.IsTutorial13Complete) return "tg_next_day";
            if (!t.IsTutorial14Complete) return "tg_delivery";
            return null;
        }

        public static string CurrentHint()
        {
            string key = CurrentKey();
            return key == null ? null : Loc.Get(key);
        }
    }
}
