using Cysharp.Threading.Tasks;
using DCL.Web3;
using DCL.Web3.Identities;
using SuperScrollView;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.Sections.Friends
{
    public class FriendsSectionController : IDisposable
    {
        private const int FRIENDS_PAGE_SIZE = 20;

        private readonly FriendsSectionView view;
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus friendEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly FriendListPagedRequestManager friendListPagedRequestManager;

        private CancellationTokenSource friendListInitCts = new ();
        private Web3Address? previousWeb3Identity;

        public FriendsSectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.view = view;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.web3IdentityCache = web3IdentityCache;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            friendListPagedRequestManager = new FriendListPagedRequestManager(friendsService, friendEventBus, FRIENDS_PAGE_SIZE);
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
        }

        public void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            friendListPagedRequestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
            friendListPagedRequestManager.OnlineFolderClicked -= FolderClicked;
            friendListPagedRequestManager.OfflineFolderClicked -= FolderClicked;
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            friendListPagedRequestManager.GetLoopListItemByIndex(loopListView, index);

        private void Enable()
        {
            previousWeb3Identity ??= web3IdentityCache.Identity?.Address;

            if (previousWeb3Identity != web3IdentityCache.Identity?.Address)
            {
                previousWeb3Identity = web3IdentityCache.Identity?.Address;
                friendListPagedRequestManager.Reset();
            }

            if (!friendListPagedRequestManager.WasInitialised)
                Init(friendListInitCts.Token).Forget();
        }

        private async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);

            friendListInitCts = friendListInitCts.SafeRestart();
            await friendListPagedRequestManager.Init(ct);

            view.SetEmptyState(!friendListPagedRequestManager.HasFriends);
            view.SetLoadingState(false);
            view.SetScrollView(friendListPagedRequestManager.HasFriends);

            if (friendListPagedRequestManager.HasFriends)
            {
                view.LoopList.SetListItemCount(friendListPagedRequestManager.GetElementsNumber(), false);
                friendListPagedRequestManager.OnlineFolderClicked += FolderClicked;
                friendListPagedRequestManager.OfflineFolderClicked += FolderClicked;
            }
        }

        private void FolderClicked()
        {
            view.LoopList.SetListItemCount(friendListPagedRequestManager.GetElementsNumber(), false);
            view.LoopList.RefreshAllShownItem();
        }

        private void Disable()
        {
            friendListInitCts = friendListInitCts.SafeRestart();
        }
    }
}
