using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using SuperScrollView;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelSectionController<T, U, K> : IDisposable
        where T : FriendPanelSectionView
        where K : FriendPanelUserView
        where U : FriendPanelRequestManager<K>
    {
        protected readonly T view;
        private readonly IWeb3IdentityCache web3IdentityCache;
        protected readonly U requestManager;

        protected CancellationTokenSource friendListInitCts = new ();
        private Web3Address? previousWeb3Identity;

        public FriendPanelSectionController(T view,
            IWeb3IdentityCache web3IdentityCache,
            U requestManager)
        {
            this.view = view;
            this.web3IdentityCache = web3IdentityCache;
            this.requestManager = requestManager;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            requestManager.ElementClicked += ElementClicked;
        }

        public virtual void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            requestManager.ElementClicked -= ElementClicked;
            requestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestManager.GetLoopListItemByIndex(loopListView, index);

        protected abstract void ElementClicked(Profile profile);

        private void Enable()
        {
            previousWeb3Identity ??= web3IdentityCache.Identity?.Address;

            if (previousWeb3Identity != web3IdentityCache.Identity?.Address)
            {
                previousWeb3Identity = web3IdentityCache.Identity?.Address;
                requestManager.Reset();
            }

            if (!requestManager.WasInitialised)
                Init(friendListInitCts.Token).Forget();
        }

        private void Disable()
        {
            friendListInitCts = friendListInitCts.SafeRestart();
        }

        protected virtual async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);
            view.SetEmptyState(false);
            view.SetScrollViewState(false);

            await requestManager.Init(ct);

            view.SetLoadingState(false);
            view.SetEmptyState(!requestManager.HasElements);
            view.SetScrollViewState(requestManager.HasElements);

            if (requestManager.HasElements)
                view.LoopList.SetListItemCount(requestManager.GetCollectionCount(), false);
        }
    }
}
