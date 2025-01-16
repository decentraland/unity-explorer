using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using SuperScrollView;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.Sections
{
    public abstract class FriendPanelSectionController<T, U, K> : IDisposable
        where T : FriendPanelSectionView
        where K : FriendPanelUserView
        where U : FriendPanelRequestManager<K>
    {
        protected readonly T view;
        protected readonly IFriendsService friendsService;
        protected readonly IFriendsEventBus friendEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        protected readonly IMVCManager mvcManager;
        protected readonly U friendListPagedRequestManager;

        protected CancellationTokenSource friendListInitCts = new ();
        private Web3Address? previousWeb3Identity;

        public FriendPanelSectionController(T view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            U friendListPagedRequestManager)
        {
            this.view = view;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.mvcManager = mvcManager;
            this.friendListPagedRequestManager = friendListPagedRequestManager;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            friendListPagedRequestManager.FriendElementClicked += FriendElementClicked;
        }

        public virtual void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            friendListPagedRequestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
            friendListPagedRequestManager.FirstFolderClicked -= FolderClicked;
            friendListPagedRequestManager.SecondFolderClicked -= FolderClicked;
        }

        private void Enable()
        {
            previousWeb3Identity ??= web3IdentityCache.Identity?.Address;

            if (previousWeb3Identity != web3IdentityCache.Identity?.Address)
            {
                previousWeb3Identity = web3IdentityCache.Identity?.Address;
                friendListPagedRequestManager.Reset();
                friendListPagedRequestManager.FirstFolderClicked -= FolderClicked;
                friendListPagedRequestManager.SecondFolderClicked -= FolderClicked;
            }

            if (!friendListPagedRequestManager.WasInitialised)
                Init(friendListInitCts.Token).Forget();
        }

        private void Disable()
        {
            friendListInitCts = friendListInitCts.SafeRestart();
        }

        protected void FolderClicked()
        {
            view.LoopList.SetListItemCount(friendListPagedRequestManager.GetElementsNumber(), false);
            view.LoopList.RefreshAllShownItem();
        }

        protected virtual async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);

            friendListInitCts = friendListInitCts.SafeRestart();
            await friendListPagedRequestManager.Init(ct);

            view.SetEmptyState(!friendListPagedRequestManager.HasElements);
            view.SetLoadingState(false);
            view.SetScrollView(friendListPagedRequestManager.HasElements);

            if (friendListPagedRequestManager.HasElements)
            {
                view.LoopList.SetListItemCount(friendListPagedRequestManager.GetElementsNumber(), false);
                view.LoopList.RefreshAllShownItem();
                friendListPagedRequestManager.FirstFolderClicked += FolderClicked;
                friendListPagedRequestManager.SecondFolderClicked += FolderClicked;
            }
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            friendListPagedRequestManager.GetLoopListItemByIndex(loopListView, index);

        protected abstract void FriendElementClicked(Profile profile);
    }
}
