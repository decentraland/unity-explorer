using DCL.Web3.Identities;
using MVC;
using SuperScrollView;
using System;

namespace DCL.Friends.UI.Sections.Requests
{
    public class RequestsSectionController : IDisposable
    {
        private const int REQUESTS_PAGE_SIZE = 20;

        private readonly RequestsSectionView view;
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IMVCManager mvcManager;
        private readonly RequestsRequestManager requestsRequestManager;

        public RequestsSectionController(RequestsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager)
        {
            this.view = view;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.mvcManager = mvcManager;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            this.requestsRequestManager = new RequestsRequestManager(friendsService, friendEventBus, REQUESTS_PAGE_SIZE);
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
        }

        public void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            requestsRequestManager.Dispose();
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestsRequestManager.GetLoopListItemByIndex(loopListView, index);

        private void Enable()
        {

        }

        private void Disable()
        {

        }
    }
}
