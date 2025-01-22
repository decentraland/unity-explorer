using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3;
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

        protected CancellationTokenSource friendListInitCts = new ();
        protected Profile? lastClickedProfile;
        private Web3Address? previousWeb3Identity;

        public FriendPanelSectionDoubleCollectionController(T view,
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
            requestManager.ElementClicked += ElementClick;
        }

        public virtual void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            requestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
            requestManager.FirstFolderClicked -= FolderClicked;
            requestManager.SecondFolderClicked -= FolderClicked;
            requestManager.ElementClicked -= ElementClick;
        }

        private void ElementClick(Profile profile)
        {
            lastClickedProfile = profile;
            ElementClicked(profile);
        }

        private void Enable()
        {
            previousWeb3Identity ??= web3IdentityCache.Identity?.Address;

            if (previousWeb3Identity != web3IdentityCache.Identity?.Address)
            {
                previousWeb3Identity = web3IdentityCache.Identity?.Address;
                requestManager.Reset();
                requestManager.FirstFolderClicked -= FolderClicked;
                requestManager.SecondFolderClicked -= FolderClicked;
            }

            if (!requestManager.WasInitialised)
                Init(friendListInitCts.Token).Forget();
        }

        private void Disable()
        {
            friendListInitCts = friendListInitCts.SafeRestart();
        }

        protected void FolderClicked()
        {
            view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
            view.LoopList.RefreshAllShownItem();
        }

        protected virtual async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);
            view.SetEmptyState(false);
            view.SetScrollViewState(false);

            friendListInitCts = friendListInitCts.SafeRestart();
            await requestManager.Init(ct);

            view.SetLoadingState(false);
            view.SetEmptyState(!requestManager.HasElements);
            view.SetScrollViewState(requestManager.HasElements);

            if (requestManager.HasElements)
            {
                view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
                requestManager.FirstFolderClicked += FolderClicked;
                requestManager.SecondFolderClicked += FolderClicked;
            }
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestManager.GetLoopListItemByIndex(loopListView, index);

        protected abstract void ElementClicked(Profile profile);
    }
}
