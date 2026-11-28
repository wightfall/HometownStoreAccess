using HometownStoreAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    /// <summary>Registers the screen contexts that exist in each scene.</summary>
    internal static class SceneSetup
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "CoreGame";

        public static void Configure(ContextManager manager, string sceneName)
        {
            manager.Clear();
            manager.Register(new KeyListContext());
            manager.Register(new SettingsContext());
            manager.Register(new OptionsContext());
            manager.Register(new SaveSlotContext());
            if (sceneName == MainMenuScene)
            {
                manager.Register(new PopupContext("confirm_menu", 100, "ctx_confirm",
                    () => MainMenu()?.ConfirmPanel()?.gameObject,
                    () => MainMenu()?.ConfirmPanel()?.messageText,
                    () => Buttons(MainMenu()?.ConfirmPanel())));
                manager.Register(new CreditsContext());
                manager.Register(new MainMenuContext());
            }
            else if (sceneName == GameScene)
            {
                GameContexts.Register(manager);
            }
            Log.Info("Accessibility contexts configured for scene '" + sceneName + "'.");
        }

        private static MainMenuControl MainMenu() => SceneCache.Get<MainMenuControl>();

        private static ConfirmationPanel ConfirmPanel(this MainMenuControl c) => c != null ? c._confirmationPanel : null;

        internal static Button[] Buttons(ConfirmationPanel p) => p == null ? null : new[] { p.Confirm, p.Cancel };
    }
}
