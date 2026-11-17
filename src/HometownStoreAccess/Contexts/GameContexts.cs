using HometownStoreAccess.Core;
using HometownStoreAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HometownStoreAccess.Contexts
{
    /// <summary>
    /// Registers every context of the main game scene. Priorities decide which one is "on top":
    /// popups (100) &gt; pause menu (90) &gt; rewards (85) &gt; dialog (80) &gt; tutorial letter (77) &gt;
    /// day summary (70) &gt; trade / lottery / vehicle (63-65) &gt; map (60) &gt; building panels (53-55) &gt;
    /// information panels (47-50) &gt; build mode (40) &gt; toolbar (11) &gt; shop floor (10).
    /// </summary>
    internal static class GameContexts
    {
        public static void Register(ContextManager m)
        {
            m.Register(new PopupContext("confirm", 100, "ctx_confirm",
                () => UIPanelManager.Instance?._confirmationPanel?.gameObject,
                () => UIPanelManager.Instance?._confirmationPanel?.messageText,
                () => SceneSetup.Buttons(UIPanelManager.Instance?._confirmationPanel)));
            m.Register(new PopupContext("notify", 99, "ctx_notice",
                () => UIPanelManager.Instance?._notiflyPanel?.gameObject,
                () => UIPanelManager.Instance?._notiflyPanel?.messageText,
                () => UIPanelManager.Instance?._notiflyPanel != null ? new Button[] { UIPanelManager.Instance._notiflyPanel.ok } : null));
            m.Register(new CongratulationContext());
            m.Register(new DialogLogContext());
            m.Register(new DialogContext());
            m.Register(new PanelContext("thanks", 78, () => SceneCache.Find("Thank For Demo"), () => Loc.Get("ctx_thanks"), includeTexts: true));
            m.Register(new PanelContext("letter", 77, LetterRoot, () => Loc.Get("ctx_letter"), includeTexts: true, skip: SkipHiddenGroup));
            m.Register(new PanelContext("tutorial_pages", 76, () => SceneCache.Find("Tutorial Temp"), () => Loc.Get("ctx_tutorial_pages"), includeTexts: true));
            m.Register(new SummaryContext());
            m.Register(new TradeContext());
            m.Register(new LotteryContext());
            m.Register(new VehicleContext());
            m.Register(new MapContext());
            m.Register(new ManufactureContext());
            m.Register(new ShelfTransferContext());
            m.Register(new CashierSkinContext());
            m.Register(new CustomerPanelContext());
            m.Register(new JournalContext());
            m.Register(new CharacterCollectionContext());
            m.Register(new StorageSummaryContext());
            m.Register(new BuildContext());
            m.Register(new ToolbarContext());
            m.Register(new FloorContext());
        }

        private static GameObject LetterRoot()
        {
            TutorialManager t = TutorialManager.Instance;
            return t != null ? t.letterPanel : null;
        }

        /// <summary>Hidden sub-panels (CanvasGroup alpha 0 or not interactable) are not part of the letter yet.</summary>
        private static bool SkipHiddenGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            return cg != null && (cg.alpha <= 0.01f || !cg.blocksRaycasts);
        }
    }
}
