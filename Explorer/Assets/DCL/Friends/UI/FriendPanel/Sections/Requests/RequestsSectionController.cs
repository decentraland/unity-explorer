using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.Friends.UI.Requests;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using DCL.Web3;
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
        private const int IDENTITY_CHANGE_POLLING_INTERVAL = 5000;

        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly ILoadingStatus loadingStatus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly CancellationTokenSource lifeCycleCts = new ();

        private Profile? lastClickedProfileCtx;
        private Web3Address? previousWeb3Identity;
        private CancellationTokenSource friendshipOperationCts = new ();

        public event Action<int>? ReceivedRequestsCountChanged;

        public RequestsSectionController(RequestsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            ILoadingStatus loadingStatus,
            RequestsRequestManager requestManager)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, requestManager)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.loadingStatus = loadingStatus;

            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, profile => Debug.Log($"Send friendship request to {profile.UserId}")))
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => Debug.Log($"Block {lastClickedProfileCtx!.UserId}")))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ReportText, view.ContextMenuSettings.ReportSprite, () => Debug.Log($"Report {lastClickedProfileCtx!.UserId}")));

            requestManager.DeleteRequestClicked += DeleteRequestClicked;
            requestManager.AcceptRequestClicked += AcceptRequestClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.RequestClicked += RequestClicked;

            friendEventBus.OnFriendRequestReceived += PropagateRequestReceived;
            friendEventBus.OnFriendRequestAccepted += PropagateRequestAcceptedRejected;
            friendEventBus.OnFriendRequestRejected += PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged += UpdateReceivedRequestsSectionCount;

            loadingStatus.CurrentStage.Subscribe(PrewarmRequests);
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.DeleteRequestClicked -= DeleteRequestClicked;
            requestManager.AcceptRequestClicked -= AcceptRequestClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.RequestClicked -= RequestClicked;
            friendEventBus.OnFriendRequestReceived -= PropagateRequestReceived;
            friendEventBus.OnFriendRequestAccepted -= PropagateRequestAcceptedRejected;
            friendEventBus.OnFriendRequestRejected -= PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged -= UpdateReceivedRequestsSectionCount;
            friendshipOperationCts.SafeCancelAndDispose();
        }

        private void RequestClicked(FriendRequest request) =>
            mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams {Request = request})).Forget();

        private void PrewarmRequests(LoadingStatus.LoadingStage stage)
        {
            if (stage != LoadingStatus.LoadingStage.Completed) return;

            async UniTaskVoid PrewarmAsync(CancellationToken ct)
            {
                await InitAsync(ct);
                loadingStatus.CurrentStage.Unsubscribe(PrewarmRequests);
                previousWeb3Identity = web3IdentityCache.Identity?.Address;
                CheckIdentityChangeAsync(lifeCycleCts.Token).Forget();
            }
            PrewarmAsync(friendshipOperationCts.Token).Forget();
        }

        private void OpenProfilePassport(Profile profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.UserId))).Forget();

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
            Debug.Log($"DeleteRequestClicked on {request.FriendRequestId}");

            async UniTaskVoid RejectFriendshipAsync(CancellationToken ct)
            {
                try
                {
                    await friendsService.RejectFriendshipAsync(request.From, ct);
                }
                catch(Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS));
                }
            }

            RejectFriendshipAsync(friendshipOperationCts.Token).Forget();
        }

        private void AcceptRequestClicked(FriendRequest request)
        {
            Debug.Log($"AcceptRequestClicked on {request.FriendRequestId}");

            async UniTaskVoid RejectFriendshipAsync(CancellationToken ct)
            {
                try
                {
                    await friendsService.AcceptFriendshipAsync(request.From, ct);
                }
                catch(Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS));
                }
            }

            RejectFriendshipAsync(friendshipOperationCts.Token).Forget();
        }

        private void ContextMenuClicked(Profile profile, Vector2 buttonPosition, RequestUserView elementView)
        {
            lastClickedProfileCtx = profile;
            userProfileContextMenuControlSettings.SetInitialData(profile, view.ChatEntryConfiguration.GetNameColor(profile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.NONE);
            elementView.CanUnHover = false;
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition, actionOnHide: () => elementView.CanUnHover = true))).Forget();
        }

        protected override async UniTask InitAsync(CancellationToken ct)
        {
            view.SetLoadingState(true);

            await requestManager.InitAsync(ct);

            view.SetLoadingState(false);

            view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
            requestManager.FirstFolderClicked += FolderClicked;
            requestManager.SecondFolderClicked += FolderClicked;

            PropagateReceivedRequestsCountChanged();
        }

        private async UniTaskVoid CheckIdentityChangeAsync(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                if (previousWeb3Identity != web3IdentityCache.Identity?.Address && web3IdentityCache.Identity?.Address != null)
                {
                    previousWeb3Identity = web3IdentityCache.Identity?.Address;
                    CheckIdentityAndReset();
                }
                else
                    await UniTask.Delay(IDENTITY_CHANGE_POLLING_INTERVAL, cancellationToken: token);
            }
        }

        protected override void ElementClicked(Profile profile)
        {
        }

    }
}
