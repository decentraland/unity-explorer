using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Requests
{
    public class RequestsRequestManager : FriendPanelDoubleCollectionRequestManager<RequestUserView>
    {
        private const int USER_ELEMENT_INDEX = 0;
        private const int STATUS_ELEMENT_INDEX = 1;
        private const int EMPTY_ELEMENT_INDEX = 2;
        private const int REQUEST_THRESHOLD = 0;

        private readonly LoopListView2 loopListView;
        private readonly CancellationTokenSource modifyRequestsCts = new ();
        private readonly List<FriendRequest> receivedRequests = new ();
        private readonly List<FriendRequest> sentRequests = new ();

        public event Action<FriendRequest>? DeleteRequestClicked;
        public event Action<FriendRequest>? AcceptRequestClicked;
        public event Action<FriendRequest>? CancelRequestClicked;
        public event Action<FriendProfile, Vector2, RequestUserView>? ContextMenuClicked;
        public event Action<FriendRequest>? RequestClicked;

        public RequestsRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            ProfileRepositoryWrapper profileDataProvider,
            int pageSize,
            LoopListView2 loopListView)
            : base(friendsService, friendEventBus, profileDataProvider, loopListView, pageSize, REQUEST_THRESHOLD, FriendPanelStatus.RECEIVED, FriendPanelStatus.SENT, STATUS_ELEMENT_INDEX, EMPTY_ELEMENT_INDEX, USER_ELEMENT_INDEX, true)
        {
            this.loopListView = loopListView;

            this.friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += ReceivedRemoved;
            this.friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser += ReceivedRemoved;
            this.friendEventBus.OnOtherUserCancelledTheRequest += ReceivedRemoved;
            this.friendEventBus.OnYouBlockedProfile += ReceivedRemoved;
            this.friendEventBus.OnYouBlockedByUser += ReceivedRemoved;

            this.friendEventBus.OnOtherUserRejectedYourRequest += SentRemoved;
            this.friendEventBus.OnOtherUserAcceptedYourRequest += SentRemoved;
            this.friendEventBus.OnYouCancelledFriendRequestSentToOtherUser += SentRemoved;
            this.friendEventBus.OnYouBlockedProfile += SentRemoved;
            this.friendEventBus.OnYouBlockedByUser += SentRemoved;

            this.friendEventBus.OnYouSentFriendRequestToOtherUser += CreateNewSentRequest;
            this.friendEventBus.OnFriendRequestReceived += CreateNewReceivedRequest;
        }

        public override void Dispose()
        {
            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= ReceivedRemoved;
            friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser -= ReceivedRemoved;
            friendEventBus.OnOtherUserCancelledTheRequest -= ReceivedRemoved;
            friendEventBus.OnYouBlockedProfile -= ReceivedRemoved;
            friendEventBus.OnYouBlockedByUser -= ReceivedRemoved;

            friendEventBus.OnOtherUserRejectedYourRequest -= SentRemoved;
            friendEventBus.OnOtherUserAcceptedYourRequest -= SentRemoved;
            friendEventBus.OnYouCancelledFriendRequestSentToOtherUser -= SentRemoved;
            friendEventBus.OnYouBlockedProfile -= SentRemoved;
            friendEventBus.OnYouBlockedByUser -= SentRemoved;

            friendEventBus.OnYouSentFriendRequestToOtherUser -= CreateNewSentRequest;
            friendEventBus.OnFriendRequestReceived -= CreateNewReceivedRequest;
            modifyRequestsCts.SafeCancelAndDispose();
        }

        private void CreateNewReceivedRequest(FriendRequest request)
        {
            if (receivedRequests.Contains(request)) return;

            receivedRequests.Add(request);
            FriendsSorter.SortFriendRequestList(receivedRequests);
            RefreshLoopList();
        }

        private void CreateNewSentRequest(FriendRequest request)
        {
            if (sentRequests.Contains(request)) return;

            sentRequests.Add(request);
            FriendsSorter.SortFriendRequestList(sentRequests);
            RefreshLoopList();
        }

        private void SentRemoved(BlockedProfile profile) =>
            SentRemoved(profile.Address);

        private void SentRemoved(string friendId)
        {
            if (sentRequests.RemoveAll(request => request.To.Address.ToString().Equals(friendId)) <= 0) return;

            RefreshLoopList();
            loopListView.ResetListView();
        }

        private void ReceivedRemoved(BlockedProfile profile) =>
            ReceivedRemoved(profile.Address);

        private void ReceivedRemoved(string friendId)
        {
            if (receivedRequests.RemoveAll(request => request.From.Address.ToString().Equals(friendId)) <= 0) return;

            RefreshLoopList();
            loopListView.ResetListView();
        }

        internal int GetReceivedRequestCount() =>
            GetFirstCollectionCount();

        protected override int GetFirstCollectionCount() =>
            receivedRequests.Count;

        protected override int GetSecondCollectionCount() =>
            sentRequests.Count;

        protected override FriendProfile GetFirstCollectionElement(int index) =>
            receivedRequests[index].From;

        protected override FriendProfile GetSecondCollectionElement(int index) =>
            sentRequests[index].To;

        protected override void CustomiseElement(RequestUserView elementView, int collectionIndex, FriendPanelStatus section)
        {
            elementView.ParentStatus = section;

            if (section != FriendPanelStatus.SENT)
            {
                elementView.DeleteButton.onClick.RemoveAllListeners();
                elementView.DeleteButton.onClick.AddListener(() => DeleteRequestClicked?.Invoke(receivedRequests[collectionIndex]));

                elementView.AcceptButton.onClick.RemoveAllListeners();
                elementView.AcceptButton.onClick.AddListener(() => AcceptRequestClicked?.Invoke(receivedRequests[collectionIndex]));
            }
            else
            {
                elementView.CancelButton.onClick.RemoveAllListeners();
                elementView.CancelButton.onClick.AddListener(() => CancelRequestClicked?.Invoke(sentRequests[collectionIndex]));
            }

            elementView.SafelyResetMainButtonListeners();
            elementView.MainButton.onClick.AddListener(() => RequestClicked?.Invoke(section == FriendPanelStatus.SENT ? sentRequests[collectionIndex] : receivedRequests[collectionIndex]));

            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile, elementView.ContextMenuButton.transform.position, elementView));

            FriendRequest request = section == FriendPanelStatus.RECEIVED ? receivedRequests[collectionIndex] : sentRequests[collectionIndex];
            elementView.RequestDate = request.Timestamp;
            elementView.HasMessageIndicator.SetActive(!string.IsNullOrEmpty(request.MessageBody));
        }

        protected override void ResetCollections()
        {
            receivedRequests.Clear();
            sentRequests.Clear();
        }

        protected override async UniTask<int> FetchDataAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            (PaginatedFriendRequestsResult received, PaginatedFriendRequestsResult sent) =
                await UniTask.WhenAll(friendsService.GetReceivedFriendRequestsAsync(pageNumber, pageSize, ct),
                    friendsService.GetSentFriendRequestsAsync(pageNumber, pageSize, ct));

            foreach (FriendRequest fr in received.Requests)
            {
                if (receivedRequests.Contains(fr)) continue;
                receivedRequests.Add(fr);
            }

            foreach (FriendRequest fr in sent.Requests)
            {
                if (sentRequests.Contains(fr)) continue;
                sentRequests.Add(fr);
            }

            return received.TotalAmount + sent.TotalAmount;
        }
    }
}
