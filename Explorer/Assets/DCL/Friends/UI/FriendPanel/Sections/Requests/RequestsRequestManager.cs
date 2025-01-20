using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends.UI.FriendPanel.Sections.Requests
{
    public class RequestsRequestManager : FriendPanelDoubleCollectionRequestManager<RequestUserView>
    {
        private const int USER_ELEMENT_INDEX = 0;
        private const int STATUS_ELEMENT_INDEX = 1;
        private const int EMPTY_ELEMENT_INDEX = 2;

        private const int MAX_REQUEST_MESSAGE_PREVIEW_LENGTH = 40;

        private readonly IProfileCache profileCache;

        private List<FriendRequest> receivedRequests = new ();
        private List<FriendRequest> sentRequests = new ();

        public event Action<Profile>? ContextMenuClicked;

        public RequestsRequestManager(IFriendsService friendsService, IFriendsEventBus friendEventBus, int pageSize, IProfileCache profileCache)
            : base(friendsService, friendEventBus, pageSize, FriendPanelStatus.RECEIVED, FriendPanelStatus.SENT, STATUS_ELEMENT_INDEX, EMPTY_ELEMENT_INDEX, USER_ELEMENT_INDEX)
        {
            this.profileCache = profileCache;
        }

        public override void Dispose()
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
            elementView.ContextMenuButton.onClick.RemoveAllListeners();
            elementView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(elementView.UserProfile));

            FriendRequest request = section == FriendPanelStatus.RECEIVED ? receivedRequests[collectionIndex] : sentRequests[collectionIndex];
            elementView.RequestDate = request.Timestamp;
            elementView.MessagePreviewText.SetText(request.MessageBody.Length > MAX_REQUEST_MESSAGE_PREVIEW_LENGTH ? $"{request.MessageBody.Substring(0, MAX_REQUEST_MESSAGE_PREVIEW_LENGTH)}..." : request.MessageBody);
        }

        protected async override UniTask FetchInitialData(CancellationToken ct)
        {

        }
    }
}
