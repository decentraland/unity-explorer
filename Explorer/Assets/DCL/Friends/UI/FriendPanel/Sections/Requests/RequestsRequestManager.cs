using Cysharp.Threading.Tasks;
using DCL.Profiles;
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

        private const int MAX_REQUEST_MESSAGE_PREVIEW_LENGTH = 23;

        private readonly IProfileCache profileCache;
        private readonly IProfileRepository profileRepository;
        private readonly LoopListView2 loopListView;
        private readonly CancellationTokenSource modifyRequestsCts = new ();

        private List<FriendRequest> receivedRequests = new ();
        private List<FriendRequest> sentRequests = new ();

        public event Action<FriendRequest>? DeleteRequestClicked;
        public event Action<FriendRequest>? AcceptRequestClicked;
        public event Action<Profile, Vector2, RequestUserView>? ContextMenuClicked;

        public RequestsRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWebRequestController webRequestController,
            int pageSize,
            IProfileCache profileCache,
            IProfileRepository profileRepository,
            LoopListView2 loopListView)
            : base(friendsService, friendEventBus, webRequestController, pageSize, FriendPanelStatus.RECEIVED, FriendPanelStatus.SENT, STATUS_ELEMENT_INDEX, EMPTY_ELEMENT_INDEX, USER_ELEMENT_INDEX)
        {
            this.profileCache = profileCache;
            this.profileRepository = profileRepository;
            this.loopListView = loopListView;

            friendEventBus.OnFriendRequestReceived += FriendRequestReceived;
            friendEventBus.OnFriendRequestSent += FriendRequestSent;
            friendEventBus.OnFriendRequestCanceled += FriendRequestRemoved;
            friendEventBus.OnFriendRequestRejected += FriendRequestRemoved;
        }

        public override void Dispose()
        {
            friendEventBus.OnFriendRequestReceived -= FriendRequestReceived;
            friendEventBus.OnFriendRequestCanceled -= FriendRequestRemoved;
            friendEventBus.OnFriendRequestRejected -= FriendRequestRemoved;
            modifyRequestsCts.SafeCancelAndDispose();
        }

        private void FriendRequestReceived(FriendRequest request)
        {
            async UniTaskVoid AddFriendRequest(FriendRequest request, CancellationToken ct)
            {
                await profileRepository.GetAsync(request.From, ct);
                receivedRequests.Add(request);
                receivedRequests.Sort((r1, r2) => r2.Timestamp.CompareTo(r1.Timestamp));
            }
            AddFriendRequest(request, modifyRequestsCts.Token).Forget();
        }

        private void FriendRequestSent(FriendRequest request)
        {
            async UniTaskVoid AddFriendRequest(FriendRequest request, CancellationToken ct)
            {
                await profileRepository.GetAsync(request.To, ct);
                sentRequests.Add(request);
                sentRequests.Sort((r1, r2) => r2.Timestamp.CompareTo(r1.Timestamp));
            }
            AddFriendRequest(request, modifyRequestsCts.Token).Forget();
        }

        private void FriendRequestRemoved(string friendId)
        {

        }

        public override int GetFirstCollectionCount() =>
            receivedRequests.Count;

        public override int GetSecondCollectionCount() =>
            sentRequests.Count;

        protected override Profile GetFirstCollectionElement(int index) =>
            profileCache.Get(receivedRequests[index].From);

        protected override Profile GetSecondCollectionElement(int index) =>
            profileCache.Get(sentRequests[index].To);

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

            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile, elementView.ContextMenuButton.transform.position, elementView));

            FriendRequest request = section == FriendPanelStatus.RECEIVED ? receivedRequests[collectionIndex] : sentRequests[collectionIndex];
            elementView.RequestDate = request.Timestamp;
            elementView.MessagePreviewText.SetText(request.MessageBody.Length > MAX_REQUEST_MESSAGE_PREVIEW_LENGTH ? $"{request.MessageBody.Substring(0, MAX_REQUEST_MESSAGE_PREVIEW_LENGTH)}..." : request.MessageBody);
        }

        protected override async UniTask FetchInitialDataAsync(CancellationToken ct)
        {
            //TODO (Lorenzo): every new friend request, also fetch the profiles to fill the cache
            receivedRequests.Add(new FriendRequest(Guid.NewGuid().ToString(), DateTime.Now.AddDays(-2), "0xd545b9e0a5f3638a5026d1914cc9b47ed16b5ae9", "0x05dE05303EAb867D51854E8b4fE03F7acb0624d9", "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Morbi gravida libero quis sapien dictum, a vehicula nisi gravida"));
            receivedRequests.Add(new FriendRequest(Guid.NewGuid().ToString(), DateTime.Now.AddDays(-1), "0xba7352cff5681b719daf33fa05e93153af8146c8", "0x05dE05303EAb867D51854E8b4fE03F7acb0624d9", "In hac habitasse platea dictumst. Proin sodales, sapien at facilisis consectetur, elit erat luctus quam, vel finibus lacus nulla vel tellus. Aenean vehicula urna nisl. Donec in lacus nisi. Aenean facilisis sagittis turpis nec finibus. Sed eu lorem arcu"));
            sentRequests.Add(new FriendRequest(Guid.NewGuid().ToString(), DateTime.Now.AddMonths(-1), "0x05dE05303EAb867D51854E8b4fE03F7acb0624d9", "0x23e3d123f69fdd7f08a7c5685506bb344a12f1c4", "Aliquam consectetur euismod dui, vel iaculis ligula rhoncus eget. Maecenas faucibus consequat eros, nec pellentesque diam volutpat ac. Quisque aliquet dolor non tellus mattis, convallis lobortis mauris lobortis"));

            await profileRepository.GetAsync("0xd545b9e0a5f3638a5026d1914cc9b47ed16b5ae9", ct);
            await profileRepository.GetAsync("0xba7352cff5681b719daf33fa05e93153af8146c8", ct);
            await profileRepository.GetAsync("0x23e3d123f69fdd7f08a7c5685506bb344a12f1c4", ct);
        }
    }
}
