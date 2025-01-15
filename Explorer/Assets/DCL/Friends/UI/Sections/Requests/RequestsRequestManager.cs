using SuperScrollView;
using System;

namespace DCL.Friends.UI.Sections.Requests
{
    public class RequestsRequestManager : IDisposable
    {
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly int pageSize;

        public RequestsRequestManager(IFriendsService friendsService, IFriendsEventBus friendEventBus, int pageSize)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.pageSize = pageSize;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            throw new NotImplementedException();
        }
    }
}
