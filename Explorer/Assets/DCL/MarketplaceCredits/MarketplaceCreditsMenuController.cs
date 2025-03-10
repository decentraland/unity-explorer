using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCreditsAPIService;
using DCL.Profiles.Self;
using DCL.SidebarBus;
using DCL.UI.Buttons;
using DCL.WebRequests;
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
        private readonly IInputBlock inputBlock;
        private readonly IWebRequestController webRequestController;

        private CancellationTokenSource showHideMenuCts;

        public MarketplaceCreditsMenuController(
            MarketplaceCreditsMenuView view,
            HoverableAndSelectableButtonWithAnimator sidebarButton,
            ISidebarBus sidebarBus,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController)
        {
            this.sidebarButton = sidebarButton;
            this.view = view;
            this.sidebarBus = sidebarBus;
            this.inputBlock = inputBlock;

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.AddListener(ClosePanel);

            marketplaceCreditsWelcomeController = new MarketplaceCreditsWelcomeController(view.WelcomeView, this, webBrowser);
            marketplaceCreditsGoalsOfTheWeekController = new MarketplaceCreditsGoalsOfTheWeekController(view.GoalsOfTheWeekView, webBrowser, marketplaceCreditsAPIClient, selfProfile, webRequestController);
        }

        public void OpenPanel()
        {
            if (view.gameObject.activeSelf)
                return;

            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.ShowAsync(showHideMenuCts.Token).Forget();
            OpenSection(MarketplaceCreditsSection.WELCOME);
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        public void ClosePanel()
        {
            if (!view.gameObject.activeSelf)
                return;

            sidebarBus.UnblockSidebar();
            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.HideAsync(showHideMenuCts.Token).Forget();
            sidebarButton.Deselect();
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        public void OpenSection(MarketplaceCreditsSection section)
        {
            switch (section)
            {
                case MarketplaceCreditsSection.WELCOME:
                    view.WelcomeView.gameObject.SetActive(true);
                    view.GoalsOfTheWeekView.gameObject.SetActive(false);
                    marketplaceCreditsWelcomeController.OnOpenSection();
                    break;
                case MarketplaceCreditsSection.GOALS_OF_THE_WEEK:
                    view.WelcomeView.gameObject.SetActive(false);
                    view.GoalsOfTheWeekView.gameObject.SetActive(true);
                    marketplaceCreditsGoalsOfTheWeekController.OnOpenSection();
                    break;
            }
        }

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
