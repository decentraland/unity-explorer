using Cysharp.Threading.Tasks;
using DCL.SidebarBus;
using DCL.UI.Buttons;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsMenuController : IDisposable
    {
        private readonly MarketplaceCreditsMenuView view;
        private readonly HoverableAndSelectableButtonWithAnimator sidebarButton;
        private readonly ISidebarBus sidebarBus;
        private readonly MarketplaceCreditsWelcomeController marketplaceCreditsWelcomeController;
        private readonly MarketplaceCreditsGoalsOfTheWeekController marketplaceCreditsGoalsOfTheWeekController;

        private CancellationTokenSource showHideMenuCts;

        public MarketplaceCreditsMenuController(
            MarketplaceCreditsMenuView view,
            HoverableAndSelectableButtonWithAnimator sidebarButton,
            ISidebarBus sidebarBus)
        {
            this.sidebarButton = sidebarButton;
            this.view = view;
            this.sidebarBus = sidebarBus;

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.AddListener(ClosePanel);

            marketplaceCreditsWelcomeController = new MarketplaceCreditsWelcomeController(view.WelcomeView, this);
            marketplaceCreditsGoalsOfTheWeekController = new MarketplaceCreditsGoalsOfTheWeekController(view.GoalsOfTheWeekView, this);
        }

        public void OpenPanel()
        {
            if (view.gameObject.activeSelf)
                return;

            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.ShowAsync(showHideMenuCts.Token).Forget();
            view.OpenSectionView(MarketplaceCreditsSection.WELCOME);
        }

        public void ClosePanel()
        {
            if (!view.gameObject.activeSelf)
                return;

            sidebarBus.UnblockSidebar();
            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.HideAsync(showHideMenuCts.Token).Forget();
            sidebarButton.Deselect();
        }

        public void OpenSectionView(MarketplaceCreditsSection section) =>
            view.OpenSectionView(section);

        public void Dispose()
        {
            showHideMenuCts.SafeCancelAndDispose();

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.RemoveAllListeners();

            marketplaceCreditsWelcomeController.Dispose();
            marketplaceCreditsGoalsOfTheWeekController.Dispose();
        }
    }
}
