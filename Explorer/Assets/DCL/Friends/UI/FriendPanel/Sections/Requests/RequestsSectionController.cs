using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
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
        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;

        private Profile? lastClickedProfileCtx;
        private CancellationTokenSource friendshipOperationCts = new ();

        public event Action<int>? ReceivedRequestsCountChanged;

        public RequestsSectionController(RequestsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            RequestsRequestManager requestManager)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, requestManager)
        {
            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: new RectOffset(15, 15, 20, 25), elementsSpacing: 5)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, profile => Debug.Log($"Send friendship request to {profile.UserId}")))
                         .AddControl(new SeparatorContextMenuControlSettings(20, -15, -15))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => Debug.Log($"Block {lastClickedProfileCtx!.UserId}")))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ReportText, view.ContextMenuSettings.ReportSprite, () => Debug.Log($"Report {lastClickedProfileCtx!.UserId}")));

            requestManager.DeleteRequestClicked += DeleteRequestClicked;
            requestManager.AcceptRequestClicked += AcceptRequestClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;

            friendEventBus.OnFriendRequestReceived += PropagateRequestReceived;
            friendEventBus.OnFriendRequestAccepted += PropagateRequestAcceptedRejected;
            friendEventBus.OnFriendRequestRejected += PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged += UpdateReceivedRequestsSectionCount;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.DeleteRequestClicked -= DeleteRequestClicked;
            requestManager.AcceptRequestClicked -= AcceptRequestClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            friendEventBus.OnFriendRequestReceived -= PropagateRequestReceived;
            friendEventBus.OnFriendRequestAccepted -= PropagateRequestAcceptedRejected;
            friendEventBus.OnFriendRequestRejected -= PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged -= UpdateReceivedRequestsSectionCount;
            friendshipOperationCts.SafeCancelAndDispose();
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

        protected override async UniTaskVoid InitAsync(CancellationToken ct)
        {
            view.SetLoadingState(true);

            await requestManager.InitAsync(ct);

            view.SetLoadingState(false);

            view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
            requestManager.FirstFolderClicked += FolderClicked;
            requestManager.SecondFolderClicked += FolderClicked;

            PropagateReceivedRequestsCountChanged();
        }

        protected override void ElementClicked(Profile profile)
        {
            Debug.Log($"ElementClicked on {profile.UserId}");
        }

    }
}
