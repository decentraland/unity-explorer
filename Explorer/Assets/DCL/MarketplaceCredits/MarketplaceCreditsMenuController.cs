using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.MarketplaceCredits.Sections;
using DCL.MarketplaceCreditsAPIService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
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
    public partial class MarketplaceCreditsMenuController : ControllerBase<MarketplaceCreditsMenuView, MarketplaceCreditsMenuController.Params>, IControllerInSharedSpace<MarketplaceCreditsMenuView, MarketplaceCreditsMenuController.Params>
    {
        private const string WEEKLY_REWARDS_INFO_LINK = "https://decentraland.org";
        private const int ERROR_NOTIFICATION_DURATION_MS = 3000;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<bool> MarketplaceCreditsOpened;

        private bool isFeatureActivated;
        private MarketplaceCreditsSection? currentSection;
        private bool isCreditsUnlockedPanelOpen;

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
        private readonly Animator sidebarCreditsButtonAnimator;
        private readonly GameObject sidebarCreditsButtonIndicator;
        private readonly IRealmData realmData;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly FeatureFlagsCache featureFlagsCache;

        private MarketplaceCreditsWelcomeSubController marketplaceCreditsWelcomeSubController;
        private MarketplaceCreditsVerifyEmailSubController marketplaceCreditsVerifyEmailSubController;
        private MarketplaceCreditsGoalsOfTheWeekSubController marketplaceCreditsGoalsOfTheWeekSubController;
        private MarketplaceCreditsWeekGoalsCompletedSubController marketplaceCreditsWeekGoalsCompletedSubController;
        private MarketplaceCreditsProgramEndedSubController marketplaceCreditsProgramEndedSubController;

        private UniTaskCompletionSource closeTaskCompletionSource;
        private CancellationTokenSource showCreditsUnlockedCts;
        private CancellationTokenSource showErrorNotificationCts;
        private CancellationTokenSource sidebarButtonStateCts;

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
            ISharedSpaceManager sharedSpaceManager,
            FeatureFlagsCache featureFlagsCache) : base(viewFactory)
        {
            this.sidebarButton = sidebarButton;
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            this.sidebarCreditsButtonAnimator = sidebarCreditsButtonAnimator;
            this.sidebarCreditsButtonIndicator = sidebarCreditsButtonIndicator;
            this.realmData = realmData;
            this.sharedSpaceManager = sharedSpaceManager;
            this.featureFlagsCache = featureFlagsCache;

            marketplaceCreditsAPIClient.OnProgramProgressUpdated += SetSidebarButtonState;
            notificationBusController.SubscribeToNotificationTypeReceived(NotificationType.CREDITS_GOAL_COMPLETED, OnMarketplaceCreditsNotificationReceived);
            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.CREDITS_GOAL_COMPLETED, OnMarketplaceCreditsNotificationClicked);

            CheckForSidebarButtonState();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.OnAnyPlaceClick += OnAnyPlaceClicked;
            viewInstance.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            viewInstance.TotalCreditsWidget.GoShoppingButton.onClick.AddListener(OpenGoShoppingLink);

            marketplaceCreditsGoalsOfTheWeekSubController = new MarketplaceCreditsGoalsOfTheWeekSubController(
                viewInstance.GoalsOfTheWeekSubView,
                marketplaceCreditsAPIClient,
                webRequestController,
                viewInstance.TotalCreditsWidget,
                this);

            marketplaceCreditsWeekGoalsCompletedSubController = new MarketplaceCreditsWeekGoalsCompletedSubController(
                viewInstance.WeekGoalsCompletedSubView);

            marketplaceCreditsProgramEndedSubController = new MarketplaceCreditsProgramEndedSubController(
                viewInstance.ProgramEndedSubView,
                webBrowser);

            marketplaceCreditsVerifyEmailSubController = new MarketplaceCreditsVerifyEmailSubController(
                viewInstance.VerifyEmailSubView,
                selfProfile,
                marketplaceCreditsAPIClient,
                this);

            marketplaceCreditsWelcomeSubController = new MarketplaceCreditsWelcomeSubController(
                viewInstance.WelcomeSubView,
                viewInstance.TotalCreditsWidget,
                this,
                marketplaceCreditsVerifyEmailSubController,
                marketplaceCreditsGoalsOfTheWeekSubController,
                marketplaceCreditsWeekGoalsCompletedSubController,
                marketplaceCreditsProgramEndedSubController,
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
            currentSection = null;
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
            viewInstance.SetInfoLinkButtonActive(true);
            currentSection = section;

            switch (section)
            {
                case MarketplaceCreditsSection.WELCOME:
                    viewInstance.SetInfoLinkButtonActive(false);
                    marketplaceCreditsWelcomeSubController.OpenSection();
                    break;
                case MarketplaceCreditsSection.VERIFY_EMAIL:
                    haveJustClaimedCredits = false;
                    marketplaceCreditsVerifyEmailSubController.OpenSection();
                    break;
                case MarketplaceCreditsSection.GOALS_OF_THE_WEEK:
                    marketplaceCreditsGoalsOfTheWeekSubController.HasToPlayClaimCreditsAnimation = haveJustClaimedCredits;
                    marketplaceCreditsGoalsOfTheWeekSubController.OpenSection();
                    break;
                case MarketplaceCreditsSection.WEEK_GOALS_COMPLETED:
                    haveJustClaimedCredits = false;
                    marketplaceCreditsWeekGoalsCompletedSubController.OpenSection();
                    break;
                case MarketplaceCreditsSection.PROGRAM_ENDED:
                    haveJustClaimedCredits = false;
                    viewInstance.TotalCreditsWidget.SetAsProgramEndVersion(isProgramEndVersion: true);
                    marketplaceCreditsProgramEndedSubController.OpenSection();
                    break;
            }

            viewInstance.TotalCreditsWidget.gameObject.SetActive(section != MarketplaceCreditsSection.WELCOME && section != MarketplaceCreditsSection.VERIFY_EMAIL);
        }

        public async UniTaskVoid ShowCreditsUnlockedPanelAsync(float claimedCredits)
        {
            isCreditsUnlockedPanelOpen = true;
            showCreditsUnlockedCts = showCreditsUnlockedCts.SafeRestart();
            await mvcManager.ShowAsync(CreditsUnlockedController.IssueCommand(new CreditsUnlockedController.Params(claimedCredits)), showCreditsUnlockedCts.Token);

            // We open the welcome section after closing the credits unlocked panel
            isCreditsUnlockedPanelOpen = false;
            haveJustClaimedCredits = true;
            OpenSection(MarketplaceCreditsSection.WELCOME);
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

            marketplaceCreditsAPIClient.OnProgramProgressUpdated -= SetSidebarButtonState;
            viewInstance!.OnAnyPlaceClick -= OnAnyPlaceClicked;
            viewInstance.InfoLinkButton.onClick.RemoveListener(OpenInfoLink);
            viewInstance.TotalCreditsWidget.GoShoppingButton.onClick.RemoveListener(OpenGoShoppingLink);

            marketplaceCreditsWelcomeSubController.Dispose();
            marketplaceCreditsVerifyEmailSubController.Dispose();
            marketplaceCreditsGoalsOfTheWeekSubController.Dispose();
            marketplaceCreditsWeekGoalsCompletedSubController.Dispose();
            marketplaceCreditsProgramEndedSubController.Dispose();
        }

        private void CloseAllSections()
        {
            marketplaceCreditsWelcomeSubController.CloseSection();
            marketplaceCreditsVerifyEmailSubController.CloseSection();
            marketplaceCreditsGoalsOfTheWeekSubController.CloseSection();
            marketplaceCreditsWeekGoalsCompletedSubController.CloseSection();
            marketplaceCreditsProgramEndedSubController.CloseSection();
        }

        private void OnAnyPlaceClicked() =>
            OnAnyPlaceClick?.Invoke();

        private void OpenInfoLink() =>
            webBrowser.OpenUrl(WEEKLY_REWARDS_INFO_LINK);

        private void OpenGoShoppingLink() =>
            webBrowser.OpenUrl(DecentralandUrl.GoShoppingWithMarketplaceCredits);

        private async UniTaskVoid ShowErrorNotificationAsync(string message, CancellationToken ct)
        {
            viewInstance!.ErrorNotification.SetText(message);
            viewInstance.ErrorNotification.Show(ct);
            await UniTask.Delay(ERROR_NOTIFICATION_DURATION_MS, cancellationToken: ct);
            viewInstance.ErrorNotification.Hide(false, ct);
        }

        private void OnMarketplaceCreditsNotificationReceived(INotification notification)
        {
            if (!isFeatureActivated)
                return;

            SetSidebarButtonAnimationAsAlert(true);
            SetSidebarButtonAsClaimIndicator(true);

            // If the user is in the Goals of the Week section, we need to refresh the information
            if (currentSection == MarketplaceCreditsSection.GOALS_OF_THE_WEEK && !isCreditsUnlockedPanelOpen)
                OpenSection(MarketplaceCreditsSection.WELCOME);
        }

        private void OnMarketplaceCreditsNotificationClicked(object[] parameters)
        {
            if (!isFeatureActivated)
                return;

            sharedSpaceManager.ShowAsync(PanelsSharingSpace.MarketplaceCredits, new Params(isOpenedFromNotification: true));
        }

        private void CheckForSidebarButtonState()
        {
            sidebarButtonStateCts = sidebarButtonStateCts.SafeRestart();
            CheckForSidebarButtonStateAsync(sidebarButtonStateCts.Token).Forget();
        }

        private async UniTaskVoid CheckForSidebarButtonStateAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.WaitUntil(() => sidebarButton.gameObject.activeInHierarchy, cancellationToken: ct);
                await UniTask.WaitUntil(() => realmData.Configured, cancellationToken: ct);
                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile == null)
                    return;

                isFeatureActivated = MarketplaceCreditsUtils.IsUserAllowedToUseTheFeatureAsync(true, ownProfile.UserId, featureFlagsCache, ct);
                if (!isFeatureActivated)
                    return;

                var creditsProgramProgressResponse = await marketplaceCreditsAPIClient.GetProgramProgressAsync(ownProfile.UserId, ct);
                SetSidebarButtonState(creditsProgramProgressResponse);
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

        private void SetSidebarButtonState(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            if (creditsProgramProgressResponse.season.timeLeft <= 0f || creditsProgramProgressResponse.season.isOutOfFunds)
            {
                SetSidebarButtonAnimationAsAlert(false);
                SetSidebarButtonAsClaimIndicator(false);
                return;
            }

            bool thereIsSomethingToClaim = creditsProgramProgressResponse.SomethingToClaim();
            SetSidebarButtonAnimationAsAlert(!creditsProgramProgressResponse.HasUserStartedProgram() || !creditsProgramProgressResponse.IsUserEmailVerified() || thereIsSomethingToClaim);
            SetSidebarButtonAsClaimIndicator(creditsProgramProgressResponse.HasUserStartedProgram() && creditsProgramProgressResponse.IsUserEmailVerified() && thereIsSomethingToClaim);
        }
    }
}
