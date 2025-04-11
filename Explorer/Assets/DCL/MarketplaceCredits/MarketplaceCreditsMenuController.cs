using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.MarketplaceCredits.Sections;
using DCL.MarketplaceCreditsAPIService;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Buttons;
using DCL.UI.SharedSpaceManager;
using DCL.WebRequests;
using ECS;
using JetBrains.Annotations;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsMenuController : ControllerBase<MarketplaceCreditsMenuView, MarketplaceCreditsMenuController.ShowParams>, IControllerInSharedSpace<MarketplaceCreditsMenuView, MarketplaceCreditsMenuController.ShowParams>
    {
        public readonly struct ShowParams
        {
            public readonly bool IsOpenedFromNotification;

            public ShowParams(bool isOpenedFromNotification)
            {
                IsOpenedFromNotification = isOpenedFromNotification;
            }
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<bool> MarketplaceCreditsOpened;

        [CanBeNull] public event IPanelInSharedSpace.ViewShowingCompleteDelegate ViewShowingComplete;
        public event Action OnAnyPlaceClick;

        private static readonly int SIDEBAR_BUTTON_ANIMATOR_IS_ALERT_ID = Animator.StringToHash("isAlert");
        private static readonly int SIDEBAR_BUTTON_ANIMATOR_IS_PAUSED_ID = Animator.StringToHash("isPaused");

        private readonly HoverableAndSelectableButtonWithAnimator sidebarButton;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private readonly IMVCManager mvcManager;
        private readonly INotificationsBusController notificationBusController;
        private readonly Animator sidebarCreditsButtonAnimator;
        private readonly GameObject sidebarCreditsButtonIndicator;
        private readonly IRealmData realmData;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private MarketplaceCreditsWelcomeController marketplaceCreditsWelcomeController;
        private MarketplaceCreditsVerifyEmailController marketplaceCreditsVerifyEmailController;
        private MarketplaceCreditsGoalsOfTheWeekController marketplaceCreditsGoalsOfTheWeekController;
        private MarketplaceCreditsWeekGoalsCompletedController marketplaceCreditsWeekGoalsCompletedController;
        private MarketplaceCreditsProgramEndedController marketplaceCreditsProgramEndedController;

        private UniTaskCompletionSource closeTaskCompletionSource;
        private CancellationTokenSource showCreditsUnlockedCts;
        private CancellationTokenSource showErrorNotificationCts;
        private CancellationTokenSource sidebarButtonStateCts;

        private Profile ownProfile;
        private bool haveJustClaimedCredits;

        public MarketplaceCreditsMenuController(
            ViewFactoryMethod viewFactory,
            HoverableAndSelectableButtonWithAnimator sidebarButton,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            INotificationsBusController notificationBusController,
            Animator sidebarCreditsButtonAnimator,
            GameObject sidebarCreditsButtonIndicator,
            IRealmData realmData,
            ISharedSpaceManager sharedSpaceManager) : base(viewFactory)
        {
            this.sidebarButton = sidebarButton;
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            this.notificationBusController = notificationBusController;
            this.sidebarCreditsButtonAnimator = sidebarCreditsButtonAnimator;
            this.sidebarCreditsButtonIndicator = sidebarCreditsButtonIndicator;
            this.realmData = realmData;
            this.sharedSpaceManager = sharedSpaceManager;

            CheckForSidebarButtonState();
        }

        protected override void OnViewInstantiated()
        {
            mvcManager.OnViewClosed += OnCreditsUnlockedPanelClosed;
            viewInstance!.OnAnyPlaceClick += OnAnyPlaceClicked;
            viewInstance.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            viewInstance.TotalCreditsWidget.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);

            notificationBusController.SubscribeToNotificationTypeReceived(NotificationType.CREDITS_GOAL_COMPLETED, OnMarketplaceCreditsNotificationReceived);
            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.CREDITS_GOAL_COMPLETED, OnMarketplaceCreditsNotificationClicked);

            marketplaceCreditsGoalsOfTheWeekController = new MarketplaceCreditsGoalsOfTheWeekController(
                viewInstance.GoalsOfTheWeekView,
                marketplaceCreditsAPIClient,
                webRequestController,
                viewInstance.TotalCreditsWidget,
                this);

            marketplaceCreditsWeekGoalsCompletedController = new MarketplaceCreditsWeekGoalsCompletedController(
                viewInstance.WeekGoalsCompletedView);

            marketplaceCreditsProgramEndedController = new MarketplaceCreditsProgramEndedController(
                viewInstance.ProgramEndedView,
                webBrowser);

            marketplaceCreditsVerifyEmailController = new MarketplaceCreditsVerifyEmailController(
                viewInstance.VerifyEmailView,
                selfProfile,
                marketplaceCreditsAPIClient,
                this);

            marketplaceCreditsWelcomeController = new MarketplaceCreditsWelcomeController(
                viewInstance.WelcomeView,
                viewInstance.TotalCreditsWidget,
                this,
                marketplaceCreditsVerifyEmailController,
                marketplaceCreditsGoalsOfTheWeekController,
                marketplaceCreditsWeekGoalsCompletedController,
                marketplaceCreditsProgramEndedController,
                webBrowser,
                marketplaceCreditsAPIClient,
                selfProfile,
                inputBlock);

            viewInstance.ErrorNotification.Hide(true, CancellationToken.None);
        }

        protected override void OnBeforeViewShow()
        {
            closeTaskCompletionSource = new UniTaskCompletionSource();
            OpenSection(MarketplaceCreditsSection.WELCOME);
            SetSidebarButtonAnimationAsPaused(true);
            MarketplaceCreditsOpened?.Invoke(inputData.IsOpenedFromNotification);
        }

        protected override void OnViewClose()
        {
            sidebarButton.Deselect();
            CloseAllSections();
            SetSidebarButtonAnimationAsPaused(false);
            haveJustClaimedCredits = false;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct), closeTaskCompletionSource.Task);
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            closeTaskCompletionSource.TrySetResult();
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        public void OpenSection(MarketplaceCreditsSection section)
        {
            CloseAllSections();

            viewInstance!.TotalCreditsWidget.SetAsProgramEndVersion(isProgramEndVersion: false);

            switch (section)
            {
                case MarketplaceCreditsSection.WELCOME:
                    marketplaceCreditsWelcomeController.OpenSection();
                    break;
                case MarketplaceCreditsSection.VERIFY_EMAIL:
                    haveJustClaimedCredits = false;
                    marketplaceCreditsVerifyEmailController.OpenSection();
                    break;
                case MarketplaceCreditsSection.GOALS_OF_THE_WEEK:
                    marketplaceCreditsGoalsOfTheWeekController.HasToPlayClaimCreditsAnimation = haveJustClaimedCredits;
                    marketplaceCreditsGoalsOfTheWeekController.OpenSection();
                    break;
                case MarketplaceCreditsSection.WEEK_GOALS_COMPLETED:
                    haveJustClaimedCredits = false;
                    marketplaceCreditsWeekGoalsCompletedController.OpenSection();
                    break;
                case MarketplaceCreditsSection.PROGRAM_ENDED:
                    haveJustClaimedCredits = false;
                    viewInstance.TotalCreditsWidget.SetAsProgramEndVersion(isProgramEndVersion: true);
                    marketplaceCreditsProgramEndedController.OpenSection();
                    break;
            }

            viewInstance.TotalCreditsWidget.gameObject.SetActive(section != MarketplaceCreditsSection.WELCOME && section != MarketplaceCreditsSection.VERIFY_EMAIL);
        }

        public void ShowCreditsUnlockedPanel(float claimedCredits)
        {
            showCreditsUnlockedCts = showCreditsUnlockedCts.SafeRestart();
            mvcManager.ShowAsync(CreditsUnlockedController.IssueCommand(new CreditsUnlockedController.Params(claimedCredits)), showCreditsUnlockedCts.Token).Forget();
        }

        public void ShowErrorNotification(string message)
        {
            showErrorNotificationCts = showErrorNotificationCts.SafeRestart();
            ShowErrorNotificationAsync(message, showErrorNotificationCts.Token).Forget();
        }

        public override void Dispose()
        {
            showCreditsUnlockedCts.SafeCancelAndDispose();
            showErrorNotificationCts.SafeCancelAndDispose();
            sidebarButtonStateCts.SafeCancelAndDispose();

            mvcManager.OnViewClosed -= OnCreditsUnlockedPanelClosed;
            viewInstance!.OnAnyPlaceClick -= OnAnyPlaceClicked;
            viewInstance.InfoLinkButton.onClick.RemoveListener(OpenInfoLink);
            viewInstance.TotalCreditsWidget.GoShoppingButton.onClick.RemoveListener(OpenLearnMoreLink);

            marketplaceCreditsWelcomeController.Dispose();
            marketplaceCreditsVerifyEmailController.Dispose();
            marketplaceCreditsGoalsOfTheWeekController.Dispose();
            marketplaceCreditsWeekGoalsCompletedController.Dispose();
            marketplaceCreditsProgramEndedController.Dispose();
        }

        private void CloseAllSections()
        {
            marketplaceCreditsWelcomeController.CloseSection();
            marketplaceCreditsVerifyEmailController.CloseSection();
            marketplaceCreditsGoalsOfTheWeekController.CloseSection();
            marketplaceCreditsWeekGoalsCompletedController.CloseSection();
            marketplaceCreditsProgramEndedController.CloseSection();
        }

        private void OnAnyPlaceClicked() =>
            OnAnyPlaceClick?.Invoke();

        private void OpenInfoLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.WEEKLY_REWARDS_INFO_LINK);

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.GO_SHOPPING_LINK);

        private async UniTaskVoid ShowErrorNotificationAsync(string message, CancellationToken ct)
        {
            viewInstance!.ErrorNotification.SetText(message);
            viewInstance.ErrorNotification.Show(ct);
            await UniTask.Delay(MarketplaceCreditsUtils.ERROR_NOTIFICATION_DURATION * 1000, cancellationToken: ct);
            viewInstance.ErrorNotification.Hide(false, ct);
        }

        private void OnCreditsUnlockedPanelClosed(IController controller)
        {
            if (controller is not CreditsUnlockedController)
                return;

            haveJustClaimedCredits = true;
            OpenSection(MarketplaceCreditsSection.WELCOME);
        }

        private void OnMarketplaceCreditsNotificationReceived(INotification notification)
        {
            SetSidebarButtonAnimationAsAlert(true);
            SetSidebarButtonAsClaimIndicator(true);
        }

        private void OnMarketplaceCreditsNotificationClicked(object[] parameters) =>
            sharedSpaceManager.ShowAsync(PanelsSharingSpace.MarketplaceCredits, new ShowParams(isOpenedFromNotification: true));

        private void CheckForSidebarButtonState()
        {
            sidebarButtonStateCts = sidebarButtonStateCts.SafeRestart();
            CheckForSidebarButtonStateAsync(sidebarButtonStateCts.Token).Forget();
        }

        private async UniTaskVoid CheckForSidebarButtonStateAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.WaitUntil(() => realmData.Configured, cancellationToken: ct);
                ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    var creditsProgramProgressResponse = await marketplaceCreditsAPIClient.GetProgramProgressAsync(ownProfile.UserId, ct);

                    if (creditsProgramProgressResponse.season.timeLeft <= 0f || creditsProgramProgressResponse.season.isOutOfFunds)
                    {
                        SetSidebarButtonAnimationAsAlert(false);
                        SetSidebarButtonAsClaimIndicator(false);
                        return;
                    }

                    bool thereIsSomethingToClaim = creditsProgramProgressResponse.SomethingToClaim();
                    SetSidebarButtonAnimationAsAlert(!creditsProgramProgressResponse.IsUserEmailVerified() || thereIsSomethingToClaim);
                    SetSidebarButtonAsClaimIndicator(creditsProgramProgressResponse.IsUserEmailVerified() && thereIsSomethingToClaim);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading the Credits Program. Please try again!";
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        public void SetSidebarButtonAnimationAsAlert(bool isOn) =>
            sidebarCreditsButtonAnimator.SetBool(SIDEBAR_BUTTON_ANIMATOR_IS_ALERT_ID, isOn);

        public void SetSidebarButtonAsClaimIndicator(bool isOn) =>
            sidebarCreditsButtonIndicator.SetActive(isOn);

        private void SetSidebarButtonAnimationAsPaused(bool isOn) =>
            sidebarCreditsButtonAnimator.SetBool(SIDEBAR_BUTTON_ANIMATOR_IS_PAUSED_ID, isOn);
    }
}
