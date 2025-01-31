using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.Friends.UI.Requests;
using DCL.RealmNavigation;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities;
using DCL.Web3.Identities;
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
        private readonly ILoadingStatus loadingStatus;
        private readonly IPassportBridge passportBridge;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly CancellationTokenSource friendshipOperationCts = new ();

        private FriendProfile? lastClickedProfileCtx;

        public event Action<int>? ReceivedRequestsCountChanged;

        public RequestsSectionController(RequestsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            ILoadingStatus loadingStatus,
            RequestsRequestManager requestManager,
            IPassportBridge passportBridge,
            IProfileThumbnailCache profileThumbnailCache,
            bool includeUserBlocking)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, requestManager)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.loadingStatus = loadingStatus;
            this.passportBridge = passportBridge;
            this.profileThumbnailCache = profileThumbnailCache;

            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, HandleContextMenuUserProfileButton))
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfileCtx!)));

            if (includeUserBlocking)
                contextMenu.AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => Debug.Log($"Block {lastClickedProfileCtx!.Address}")));

            requestManager.DeleteRequestClicked += DeleteRequestClicked;
            requestManager.AcceptRequestClicked += AcceptRequestClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.RequestClicked += RequestClicked;

            friendEventBus.OnFriendRequestReceived += PropagateRequestReceived;
            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += PropagateRequestAcceptedRejected;
            friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser += PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged += UpdateReceivedRequestsSectionCount;

            loadingStatus.CurrentStage.Subscribe(PrewarmRequests);
            web3IdentityCache.OnIdentityChanged += ResetAndInit;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.DeleteRequestClicked -= DeleteRequestClicked;
            requestManager.AcceptRequestClicked -= AcceptRequestClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.RequestClicked -= RequestClicked;
            friendEventBus.OnFriendRequestReceived -= PropagateRequestReceived;
            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= PropagateRequestAcceptedRejected;
            friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser -= PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged -= UpdateReceivedRequestsSectionCount;
            friendshipOperationCts.SafeCancelAndDispose();
            web3IdentityCache.OnIdentityChanged -= ResetAndInit;
        }

        private void HandleContextMenuUserProfileButton(string userId, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            if (friendshipStatus == UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT)
                CancelFriendshipRequestAsync(friendshipOperationCts.Token).Forget();
            else if (friendshipStatus == UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED)
                AcceptFriendshipAsync(friendshipOperationCts.Token).Forget();

            return;

            async UniTaskVoid CancelFriendshipRequestAsync(CancellationToken ct)
            {
                await friendsService.CancelFriendshipAsync(userId, ct);
            }

            async UniTaskVoid AcceptFriendshipAsync(CancellationToken ct)
            {
                await friendsService.AcceptFriendshipAsync(userId, ct);
            }
        }

        private void ResetAndInit()
        {
            ResetState();
            PropagateReceivedRequestsCountChanged();
            CheckShouldInit();
        }

        private void RequestClicked(FriendRequest request) =>
            mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams {Request = request})).Forget();

        private void PrewarmRequests(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            PrewarmAsync(friendshipOperationCts.Token).Forget();

            async UniTaskVoid PrewarmAsync(CancellationToken ct)
            {
                await InitAsync(ct);
                loadingStatus.CurrentStage.Unsubscribe(PrewarmRequests);
            }
        }

        private void OpenProfilePassport(FriendProfile profile) =>
            passportBridge.ShowAsync(profile.Address).Forget();

        private void PropagateRequestReceived(FriendRequest request) =>
            PropagateReceivedRequestsCountChanged();

        private void PropagateRequestAcceptedRejected(string userId) =>
            PropagateReceivedRequestsCountChanged();

        private void PropagateReceivedRequestsCountChanged() =>
            ReceivedRequestsCountChanged?.Invoke(requestManager.GetFirstCollectionCount());

        private void UpdateReceivedRequestsSectionCount(int count) =>
            view.TabNotificationIndicator.SetNotificationCount(count);

        private void DeleteRequestClicked(FriendRequest request)
        {
            RejectFriendshipAsync(friendshipOperationCts.Token).Forget();

            async UniTaskVoid RejectFriendshipAsync(CancellationToken ct)
            {
                try
                {
                    await friendsService.RejectFriendshipAsync(request.From.Address, ct);
                }
                catch(Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS));
                }
            }
        }

        private void AcceptRequestClicked(FriendRequest request)
        {
            AcceptFriendshipAsync(friendshipOperationCts.Token).Forget();

            async UniTaskVoid AcceptFriendshipAsync(CancellationToken ct)
            {
                try
                {
                    await friendsService.AcceptFriendshipAsync(request.From.Address, ct);
                }
                catch(Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS));
                }
            }
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, RequestUserView elementView)
        {
            lastClickedProfileCtx = friendProfile;
            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                view.ChatEntryConfiguration.GetNameColor(friendProfile.Name),
                elementView.ParentStatus == FriendPanelStatus.SENT ? UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT : UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED,
                profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString()));
            elementView.CanUnHover = false;
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition, actionOnHide: () => elementView.CanUnHover = true, closeTask: panelLifecycleTask?.Task))).Forget();
        }

        protected override async UniTask InitAsync(CancellationToken ct)
        {
            view.SetLoadingState(true);
            view.SetScrollViewState(false);

            await requestManager.InitAsync(ct);

            view.SetLoadingState(false);
            view.SetScrollViewState(true);

            RefreshLoopList();
            requestManager.FirstFolderClicked += FolderClicked;
            requestManager.SecondFolderClicked += FolderClicked;

            PropagateReceivedRequestsCountChanged();
        }

        protected override void ElementClicked(FriendProfile profile)
        {
        }

    }
}
