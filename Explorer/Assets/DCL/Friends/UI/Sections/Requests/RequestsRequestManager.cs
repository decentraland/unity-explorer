using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends.UI.Sections.Requests
{
    public class RequestsRequestManager : FriendPanelRequestManager<RequestUserView>
    {
        private const int USER_ELEMENT_INDEX = 0;
        private const int STATUS_ELEMENT_INDEX = 1;
        private const int EMPTY_ELEMENT_INDEX = 2;

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

        protected override void CustomiseElement(RequestUserView element)
        {
            element.ContextMenuButton.onClick.RemoveAllListeners();
            element.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(element.UserProfile));
            //TODO (Lorenzo): set the request date
            // requestUserView.RequestDate = ???
        }

        protected async override UniTask FetchInitialData(CancellationToken ct)
        {

        }
    }
}
