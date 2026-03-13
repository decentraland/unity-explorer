using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.Requests;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.Controls.Configs;
using DCL.Utilities.Extensions;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Requests
{
    public class RequestsSectionController : FriendPanelSectionDoubleCollectionController<RequestsSectionView, RequestsRequestManager, RequestUserView>
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;
        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IPassportBridge passportBridge;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ISelfProfile selfProfile;

        private CancellationTokenSource friendshipOperationCts = new ();
        private CancellationTokenSource? reportConfirmationDialogCts;
        private Profile.CompactInfo? lastClickedProfileCtx;

        public event Action<int>? ReceivedRequestsCountChanged;

        public RequestsSectionController(RequestsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            RequestsRequestManager requestManager,
            IPassportBridge passportBridge,
            bool includeUserBlocking,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISelfProfile selfProfile)
            : base(view, friendsService, friendEventBus, mvcManager, requestManager)
        {
            this.passportBridge = passportBridge;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.selfProfile = selfProfile;

            ColorUtility.TryParseHtmlString("#FF2D55", out Color redColor);
            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(HandleContextMenuUserProfileButton))
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfileCtx!.Value)))
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => BlockUserClicked(lastClickedProfileCtx!.Value), iconColor: redColor, textColor: redColor), includeUserBlocking));

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.REPORT_USER))
                contextMenu.AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ReportText, view.ContextMenuSettings.ReportSprite, () => ReportUserClicked(lastClickedProfileCtx!.Value), iconColor: redColor, textColor: redColor));

            requestManager.DeleteRequestClicked += DeleteRequestClicked;
            requestManager.AcceptRequestClicked += AcceptRequestClicked;
            requestManager.CancelRequestClicked += CancelRequestClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.RequestClicked += RequestClicked;

            friendEventBus.OnFriendRequestReceived += PropagateRequestReceived;
            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += PropagateReceivedRequestsCountChanged;
            friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser += PropagateReceivedRequestsCountChanged;
            friendEventBus.OnOtherUserCancelledTheRequest += PropagateReceivedRequestsCountChanged;
            friendEventBus.OnYouBlockedProfile += PropagateRequestReceived;
            friendEventBus.OnYouBlockedByUser += PropagateReceivedRequestsCountChanged;

            ReceivedRequestsCountChanged += UpdateReceivedRequestsSectionCount;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.DeleteRequestClicked -= DeleteRequestClicked;
            requestManager.AcceptRequestClicked -= AcceptRequestClicked;
            requestManager.CancelRequestClicked -= CancelRequestClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.RequestClicked -= RequestClicked;
            friendEventBus.OnFriendRequestReceived -= PropagateRequestReceived;
            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= PropagateReceivedRequestsCountChanged;
            friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser -= PropagateReceivedRequestsCountChanged;
            friendEventBus.OnOtherUserCancelledTheRequest -= PropagateReceivedRequestsCountChanged;
            friendEventBus.OnYouBlockedProfile -= PropagateRequestReceived;
            friendEventBus.OnYouBlockedByUser -= PropagateReceivedRequestsCountChanged;

            ReceivedRequestsCountChanged -= UpdateReceivedRequestsSectionCount;
            friendshipOperationCts.SafeCancelAndDispose();
            reportConfirmationDialogCts.SafeCancelAndDispose();
        }

        public override void Reset()
        {
            base.Reset();

            PropagateReceivedRequestsCountChanged();
            CheckShouldInit();
        }

        private void BlockUserClicked(Profile.CompactInfo profile) =>
            FriendListSectionUtilities.BlockUserClicked(mvcManager, profile.Address, profile.Name);

        private void ReportUserClicked(Profile.CompactInfo profile)
        {
            reportConfirmationDialogCts = reportConfirmationDialogCts.SafeRestart();
            ShowReportConfirmationDialogAsync(profile, reportConfirmationDialogCts.Token).Forget();
            return;

            async UniTask ShowReportConfirmationDialogAsync(Profile.CompactInfo userProfile, CancellationToken ct)
            {
                try
                {
                    bool confirmed = await ReportUserConfirmationDialog.ShowAsync(
                        ViewDependencies.ConfirmationDialogOpener,
                        userProfile.Name,
                        view.ContextMenuSettings.ReportSprite,
                        ReportCategory.FRIENDS,
                        ct);

                    if (!confirmed)
                        return;

                    Profile? ownProfile = await selfProfile.ProfileAsync(ct);

                    webBrowser.OpenUrl(string.Format(decentralandUrlsSource.Url(DecentralandUrl.ReportUserForm),
                        ownProfile != null ? ownProfile.UserId : string.Empty,
                        userProfile.UserId));
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.FRIENDS); }
            }
        }

        private void HandleContextMenuUserProfileButton(Profile.CompactInfo userData, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            if (friendshipStatus == UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT)
                CancelFriendshipRequestAsync(friendshipOperationCts.Token).Forget();
            else if (friendshipStatus == UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED)
                mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams { OneShotFriendAccepted = lastClickedProfileCtx }), ct: friendshipOperationCts.Token).Forget();

            return;

            async UniTaskVoid CancelFriendshipRequestAsync(CancellationToken ct)
            {
                await friendsService.CancelFriendshipAsync(userData.UserId, ct).SuppressToResultAsync(ReportCategory.FRIENDS);
            }
        }

        private void RequestClicked(FriendRequest request) =>
            mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams {Request = request})).Forget();

        private void OpenProfilePassport(Profile.CompactInfo profile) =>
            passportBridge.ShowAsync(profile.Address).Forget();

        private void PropagateRequestReceived(FriendRequest request) =>
            PropagateReceivedRequestsCountChanged();

        private void PropagateRequestReceived(BlockedProfile profile) =>
            PropagateReceivedRequestsCountChanged();

        private void PropagateReceivedRequestsCountChanged(string userId) =>
            PropagateReceivedRequestsCountChanged();

        private void PropagateReceivedRequestsCountChanged() =>
            ReceivedRequestsCountChanged?.Invoke(requestManager.GetReceivedRequestCount());

        private void UpdateReceivedRequestsSectionCount(int count) =>
            view.TabNotificationIndicator.SetNotificationCount(count);

        private void DeleteRequestClicked(FriendRequest request)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            RejectFriendshipAsync(friendshipOperationCts.Token).Forget();

            async UniTaskVoid RejectFriendshipAsync(CancellationToken ct)
            {
                await friendsService.RejectFriendshipAsync(request.From.Address, ct).SuppressToResultAsync(ReportCategory.FRIENDS);
            }
        }

        private void CancelRequestClicked(FriendRequest request)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            CancelFriendshipAsync(friendshipOperationCts.Token).Forget();

            async UniTaskVoid CancelFriendshipAsync(CancellationToken ct)
            {
                await friendsService.CancelFriendshipAsync(request.To.Address, ct).SuppressToResultAsync(ReportCategory.FRIENDS);
            }
        }

        private void AcceptRequestClicked(FriendRequest request)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams { OneShotFriendAccepted = request.From}), ct: friendshipOperationCts.Token).Forget();
        }

        private void ContextMenuClicked(Profile.CompactInfo friendProfile, Vector2 buttonPosition, RequestUserView elementView)
        {
            lastClickedProfileCtx = friendProfile;

            userProfileContextMenuControlSettings.SetInitialData(friendProfile,
                elementView.ParentStatus == FriendPanelStatus.SENT ? UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT : UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED);
            elementView.CanUnHover = false;
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition,
                actionOnHide: () => elementView.CanUnHover = true,
                closeTask: panelLifecycleTask?.Task)))
                      .Forget();
        }

        protected override void OnLoopListInitialized()
        {
            PropagateReceivedRequestsCountChanged();
        }

        protected override bool ShouldShowScrollView() =>
            true; // the request section should always present 2 lists (sent/received), even if it's empty

        protected override void ElementClicked(Profile.CompactInfo profile)
        {
        }
    }
}
