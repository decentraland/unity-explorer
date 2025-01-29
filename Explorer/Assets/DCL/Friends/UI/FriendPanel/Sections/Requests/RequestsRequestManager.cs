using Cysharp.Threading.Tasks;
using DCL.WebRequests;
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

        private readonly LoopListView2 loopListView;
        private readonly CancellationTokenSource modifyRequestsCts = new ();
        private readonly List<FriendRequest> receivedRequests = new ();
        private readonly List<FriendRequest> sentRequests = new ();

        public event Action<FriendRequest>? DeleteRequestClicked;
        public event Action<FriendRequest>? AcceptRequestClicked;
        public event Action<FriendProfile, Vector2, RequestUserView>? ContextMenuClicked;
        public event Action<FriendRequest>? RequestClicked;

        public RequestsRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWebRequestController webRequestController,
            IProfileThumbnailCache profileThumbnailCache,
            int pageSize,
            LoopListView2 loopListView)
            : base(friendsService, friendEventBus, webRequestController, profileThumbnailCache, pageSize, FriendPanelStatus.RECEIVED, FriendPanelStatus.SENT, STATUS_ELEMENT_INDEX, EMPTY_ELEMENT_INDEX, USER_ELEMENT_INDEX)
        {
            this.loopListView = loopListView;

            this.friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += ReceivedRemoved;
            this.friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser += ReceivedRemoved;
            this.friendEventBus.OnOtherUserRemovedTheRequest += ReceivedRemoved;
            this.friendEventBus.OnOtherUserCancelledTheRequest += ReceivedRemoved;

            this.friendEventBus.OnOtherUserRejectedYourRequest += SentRemoved;
            this.friendEventBus.OnOtherUserAcceptedYourRequest += SentRemoved;
            this.friendEventBus.OnYouCancelledFriendRequestSentToOtherUser += SentRemoved;

            this.friendEventBus.OnYouSentFriendRequestToOtherUser += CreateNewSentRequest;
            this.friendEventBus.OnFriendRequestReceived += CreateNewReceivedRequest;
        }

        public override void Dispose()
        {
            friendEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= ReceivedRemoved;
            friendEventBus.OnYouRejectedFriendRequestReceivedFromOtherUser -= ReceivedRemoved;
            friendEventBus.OnOtherUserRemovedTheRequest -= ReceivedRemoved;
            friendEventBus.OnOtherUserCancelledTheRequest -= ReceivedRemoved;

            friendEventBus.OnOtherUserRejectedYourRequest -= SentRemoved;
            friendEventBus.OnOtherUserAcceptedYourRequest -= SentRemoved;
            friendEventBus.OnYouCancelledFriendRequestSentToOtherUser -= SentRemoved;

            friendEventBus.OnYouSentFriendRequestToOtherUser -= CreateNewSentRequest;
            friendEventBus.OnFriendRequestReceived -= CreateNewReceivedRequest;
            modifyRequestsCts.SafeCancelAndDispose();
        }

        private void CreateNewReceivedRequest(FriendRequest request)
        {
            if (receivedRequests.Contains(request)) return;

            receivedRequests.Add(request);
            receivedRequests.Sort((r1, r2) => r2.Timestamp.CompareTo(r1.Timestamp));
            loopListView.RefreshAllShownItem();
        }

        private void CreateNewSentRequest(FriendRequest request)
        {
            if (sentRequests.Contains(request)) return;

            sentRequests.Add(request);
            sentRequests.Sort((r1, r2) => r2.Timestamp.CompareTo(r1.Timestamp));
            loopListView.RefreshAllShownItem();
        }

        private void SentRemoved(string friendId)
        {
            sentRequests.RemoveAll(request => request.To.Address.ToString().Equals(friendId));
            loopListView.RefreshAllShownItem();
        }

        private void ReceivedRemoved(string friendId)
        {
            receivedRequests.RemoveAll(request => request.To.Address.ToString().Equals(friendId));
            loopListView.RefreshAllShownItem();
        }

        public override int GetFirstCollectionCount() =>
            receivedRequests.Count;

        public override int GetSecondCollectionCount() =>
            sentRequests.Count;

        protected override FriendProfile GetFirstCollectionElement(int index) =>
            receivedRequests[index].From;

        protected override FriendProfile GetSecondCollectionElement(int index) =>
            sentRequests[index].To;

        protected override void CustomiseElement(RequestUserView elementView, int collectionIndex, FriendPanelStatus section)
        {
            if (section == FriendPanelStatus.SENT)
                elementView.InhibitInteractionButtons();
            else
            {
                elementView.DeleteButton.onClick.RemoveAllListeners();
                elementView.DeleteButton.onClick.AddListener(() => DeleteRequestClicked?.Invoke(receivedRequests[collectionIndex]));

                elementView.AcceptButton.onClick.RemoveAllListeners();
                elementView.AcceptButton.onClick.AddListener(() => AcceptRequestClicked?.Invoke(receivedRequests[collectionIndex]));
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

        protected override async UniTask FetchInitialDataAsync(CancellationToken ct)
        {
            (PaginatedFriendRequestsResult received, PaginatedFriendRequestsResult sent) =
                await UniTask.WhenAll(friendsService.GetReceivedFriendRequestsAsync(1, 100, ct),
                    friendsService.GetSentFriendRequestsAsync(1, 100, ct));

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

            // FriendProfile friendProfile1 = new FriendProfile(new Web3Address("0xd545b9e0a5f3638a5026d1914cc9b47ed16b5ae9"), "Test1", false, URLAddress.EMPTY);
            // FriendProfile friendProfile2 = new FriendProfile(new Web3Address("0xba7352cff5681b719daf33fa05e93153af8146c8"), "Test2", false, URLAddress.EMPTY);
            // FriendProfile friendProfile3 = new FriendProfile(new Web3Address("0x23e3d123f69fdd7f08a7c5685506bb344a12f1c4"), "Test3", true, URLAddress.EMPTY);
            // FriendProfile userFriendProfile = new FriendProfile(new Web3Address("0x31d4f4dd8615ec45bbb6330da69f60032aca219e"), "MyUser", true, URLAddress.EMPTY);
            //
            // receivedRequests.Add(new FriendRequest(Guid.NewGuid().ToString(), DateTime.Now.AddDays(-2), friendProfile1, userFriendProfile, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Morbi gravida libero quis sapien dictum, a vehicula nisi gravida"));
            // receivedRequests.Add(new FriendRequest(Guid.NewGuid().ToString(), DateTime.Now.AddDays(-1), friendProfile2, userFriendProfile, "In hac habitasse platea dictumst. Proin sodales, sapien at facilisis consectetur, elit erat luctus quam, vel finibus lacus nulla vel tellus. Aenean vehicula urna nisl. Donec in lacus nisi. Aenean facilisis sagittis turpis nec finibus. Sed eu lorem arcu"));
            // sentRequests.Add(new FriendRequest(Guid.NewGuid().ToString(), DateTime.Now.AddMonths(-1), userFriendProfile, friendProfile3, "Aliquam consectetur euismod dui, vel iaculis ligula rhoncus eget. Maecenas faucibus consequat eros, nec pellentesque diam volutpat ac. Quisque aliquet dolor non tellus mattis, convallis lobortis mauris lobortis"));
        }
    }
}
