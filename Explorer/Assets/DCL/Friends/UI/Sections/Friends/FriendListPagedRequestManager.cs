using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends.UI.Sections.Friends
{
    public class FriendListPagedRequestManager : IDisposable
    {
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly int pageSize;

        private int pageNumber = 0;
        private int totalFetched = 0;
        private List<Profile> friends = new ();

        public bool HasFriends { get; private set; }
        public bool WasInitialised { get; private set; }

        public FriendListPagedRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            int pageSize)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.pageSize = pageSize;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public async UniTask Init(CancellationToken ct)
        {
            PaginatedFriendsResult result = await friendsService.GetFriendsAsync(pageNumber, pageSize, ct);
            HasFriends = result.TotalAmount > 0;
            friends.AddRange(result.Friends);
            WasInitialised = true;
        }
    }
}
