using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using MVC;
using SuperScrollView;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelSectionDoubleCollectionController<T, U, K> : IDisposable
        where T : FriendPanelSectionView
        where K : FriendPanelUserView
        where U : FriendPanelDoubleCollectionRequestManager<K>
    {
        protected readonly T view;
        protected readonly IFriendsService friendsService;
        protected readonly IFriendsEventBus friendEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        protected readonly IMVCManager mvcManager;
        protected readonly U requestManager;

        private CancellationTokenSource friendListInitCts = new ();

        protected FriendPanelSectionDoubleCollectionController(T view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            U requestManager)
        {
            this.view = view;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.mvcManager = mvcManager;
            this.requestManager = requestManager;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            requestManager.ElementClicked += ElementClicked;
            web3IdentityCache.OnIdentityChanged += ResetState;
        }

        public virtual void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            requestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
            requestManager.FirstFolderClicked -= FolderClicked;
            requestManager.SecondFolderClicked -= FolderClicked;
            requestManager.ElementClicked -= ElementClicked;
            web3IdentityCache.OnIdentityChanged -= ResetState;
        }

        protected void ResetState()
        {
            requestManager.Reset();
            requestManager.FirstFolderClicked -= FolderClicked;
            requestManager.SecondFolderClicked -= FolderClicked;
        }

        protected void CheckShouldInit()
        {
            if (!requestManager.WasInitialised)
                InitAsync(friendListInitCts.Token).Forget();
        }

        private void Enable() =>
            CheckShouldInit();

        private void Disable() =>
            friendListInitCts = friendListInitCts.SafeRestart();

        protected void FolderClicked()
        {
            RefreshLoopList();
        }

        protected void RefreshLoopList()
        {
            view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
            view.LoopList.RefreshAllShownItem();
        }

        protected virtual async UniTask InitAsync(CancellationToken ct)
        {
            view.SetLoadingState(true);
            view.SetEmptyState(false);
            view.SetScrollViewState(false);

            await requestManager.InitAsync(ct);

            view.SetLoadingState(false);
            view.SetEmptyState(!requestManager.HasElements);
            view.SetScrollViewState(requestManager.HasElements);

            if (requestManager.HasElements)
            {
                RefreshLoopList();
                requestManager.FirstFolderClicked += FolderClicked;
                requestManager.SecondFolderClicked += FolderClicked;
            }
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestManager.GetLoopListItemByIndex(loopListView, index);

        protected abstract void ElementClicked(FriendProfile profile);
    }
}
