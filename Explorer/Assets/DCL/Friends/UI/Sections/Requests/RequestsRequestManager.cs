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

        public event Action<Profile> ContextMenuClicked;

        public RequestsRequestManager(IFriendsService friendsService, IFriendsEventBus friendEventBus, int pageSize, IProfileCache profileCache)
            : base(friendsService, friendEventBus, pageSize, FriendPanelStatus.ONLINE, FriendPanelStatus.OFFLINE, STATUS_ELEMENT_INDEX, EMPTY_ELEMENT_INDEX, USER_ELEMENT_INDEX)
        {
            this.profileCache = profileCache;
            ConfigureAccessors(GetReceivedRequest, GetSentRequest);
            SetElementCustomizer(requestUserView =>
            {
                requestUserView.ContextMenuButton.onClick.RemoveAllListeners();
                requestUserView.ContextMenuButton.onClick.AddListener(() => ContextMenuClicked?.Invoke(requestUserView.UserProfile));
                //TODO (Lorenzo): set the request date
                // requestUserView.RequestDate = ???
            });
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        private Profile GetReceivedRequest(int index) =>
            profileCache.Get(receivedRequests[index].From);

        private Profile GetSentRequest(int index) =>
            profileCache.Get(sentRequests[index].To);

        protected override int GetFirstCollectionCount() =>
            receivedRequests.Count;

        protected override int GetSecondCollectionCount() =>
            sentRequests.Count;

        protected async override UniTask FetchInitialData(CancellationToken ct)
        {

        }
    }
}
