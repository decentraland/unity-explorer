using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCredits.Sections;
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
        private readonly MarketplaceCreditsWeekGoalsCompletedController marketplaceCreditsWeekGoalsCompletedController;
        private readonly MarketplaceCreditsProgramEndedController marketplaceCreditsProgramEndedController;
        private readonly IInputBlock inputBlock;
        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;

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
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;

            view.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.AddListener(ClosePanel);

            marketplaceCreditsWelcomeController = new MarketplaceCreditsWelcomeController(
                view.WelcomeView,
                view.TotalCreditsWidget,
                this,
                webBrowser);

            marketplaceCreditsWeekGoalsCompletedController = new MarketplaceCreditsWeekGoalsCompletedController(
                view.WeekGoalsCompletedView,
                view.TotalCreditsWidget);

            marketplaceCreditsProgramEndedController = new MarketplaceCreditsProgramEndedController(
                view.ProgramEndedView,
                view.TotalCreditsWidget);

            marketplaceCreditsGoalsOfTheWeekController = new MarketplaceCreditsGoalsOfTheWeekController(
                view.GoalsOfTheWeekView,
                view.TotalCreditsWidget,
                webBrowser,
                marketplaceCreditsAPIClient,
                selfProfile,
                webRequestController,
                this,
                marketplaceCreditsWeekGoalsCompletedController);
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
                    view.WeekGoalsCompletedView.gameObject.SetActive(false);
                    view.ProgramEndedView.gameObject.SetActive(false);
                    marketplaceCreditsWelcomeController.OnOpenSection();
                    break;
                case MarketplaceCreditsSection.GOALS_OF_THE_WEEK:
                    view.WelcomeView.gameObject.SetActive(false);
                    view.GoalsOfTheWeekView.gameObject.SetActive(true);
                    view.WeekGoalsCompletedView.gameObject.SetActive(false);
                    view.ProgramEndedView.gameObject.SetActive(false);
                    marketplaceCreditsGoalsOfTheWeekController.OnOpenSection();
                    break;
                case MarketplaceCreditsSection.WEEK_GOALS_COMPLETED:
                    view.WelcomeView.gameObject.SetActive(false);
                    view.GoalsOfTheWeekView.gameObject.SetActive(false);
                    view.WeekGoalsCompletedView.gameObject.SetActive(true);
                    view.ProgramEndedView.gameObject.SetActive(false);
                    marketplaceCreditsWeekGoalsCompletedController.OnOpenSection();
                    break;
                case MarketplaceCreditsSection.PROGRAM_ENDED:
                    view.WelcomeView.gameObject.SetActive(false);
                    view.GoalsOfTheWeekView.gameObject.SetActive(false);
                    view.WeekGoalsCompletedView.gameObject.SetActive(false);
                    view.ProgramEndedView.gameObject.SetActive(true);
                    marketplaceCreditsProgramEndedController.OnOpenSection();
                    break;
            }
        }

        public void Dispose()
        {
            showHideMenuCts.SafeCancelAndDispose();

            view.InfoLinkButton.onClick.RemoveListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.RemoveListener(OpenLearnMoreLink);

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.RemoveListener(ClosePanel);

            marketplaceCreditsWelcomeController.Dispose();
            marketplaceCreditsGoalsOfTheWeekController.Dispose();
            marketplaceCreditsWeekGoalsCompletedController.Dispose();
            marketplaceCreditsProgramEndedController.Dispose();
        }

        private void OpenInfoLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.INFO_LINK);

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.GO_SHOPPING_LINK);
    }
}
