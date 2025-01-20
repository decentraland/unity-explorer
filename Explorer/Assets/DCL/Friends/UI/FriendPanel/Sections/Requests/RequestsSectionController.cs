using Cysharp.Threading.Tasks;
using DCL.Profiles;
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
        public event Action<int>? ReceivedRequestsCountChanged;

        public RequestsSectionController(RequestsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            RequestsRequestManager requestManager)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, requestManager)
        {
            requestManager.ContextMenuClicked += ContextMenuClicked;
            friendEventBus.OnFriendRequestReceived += PropagateRequestReceived;
            friendEventBus.OnFriendRequestAccepted += PropagateRequestAcceptedRejected;
            friendEventBus.OnFriendRequestRejected += PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged += UpdateReceivedRequestsSectionCount;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            friendEventBus.OnFriendRequestReceived -= PropagateRequestReceived;
            friendEventBus.OnFriendRequestAccepted -= PropagateRequestAcceptedRejected;
            friendEventBus.OnFriendRequestRejected -= PropagateRequestAcceptedRejected;

            ReceivedRequestsCountChanged -= UpdateReceivedRequestsSectionCount;
        }

        private void PropagateRequestReceived(FriendRequest request) =>
            PropagateReceivedRequestsCountChanged();

        private void PropagateRequestAcceptedRejected(string userId) =>
            PropagateReceivedRequestsCountChanged();

        private void PropagateReceivedRequestsCountChanged() =>
            ReceivedRequestsCountChanged?.Invoke(requestManager.GetFirstCollectionCount());

        private void UpdateReceivedRequestsSectionCount(int count) =>
            view.TabNotificationIndicator.SetNotificationCount(count);

        private void ContextMenuClicked(Profile profile)
        {
            Debug.Log($"ContextMenuClicked on {profile.UserId}");
        }

        protected override async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);

            friendListInitCts = friendListInitCts.SafeRestart();
            await requestManager.Init(ct);

            view.SetLoadingState(false);

            view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
            view.LoopList.RefreshAllShownItem();
            requestManager.FirstFolderClicked += FolderClicked;
            requestManager.SecondFolderClicked += FolderClicked;
        }

        protected override void ElementClicked(Profile profile)
        {

        }

    }
}
