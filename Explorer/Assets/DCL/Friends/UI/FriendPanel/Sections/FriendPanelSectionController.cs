using Cysharp.Threading.Tasks;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelSectionController<T, U, K> : IDisposable
        where T : FriendPanelSectionView
        where K : FriendPanelUserView
        where U : FriendPanelRequestManager<K>
    {
        protected readonly T view;
        protected readonly U requestManager;

        protected UniTaskCompletionSource? panelLifecycleTask;
        private CancellationTokenSource friendListInitCts = new ();

        protected FriendPanelSectionController(T view,
            U requestManager)
        {
            this.view = view;
            this.requestManager = requestManager;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            requestManager.ElementClicked += ElementClicked;
            this.view.LoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public virtual void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            requestManager.ElementClicked -= ElementClicked;
            requestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
        }

        public void Reset() =>
            requestManager.Reset();

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestManager.GetLoopListItemByIndex(loopListView, index);

        protected abstract void ElementClicked(FriendProfile profile);

        private void Enable()
        {
            view.LoopList.ResetListView();
            panelLifecycleTask = new UniTaskCompletionSource();
            if (!requestManager.WasInitialised)
                InitAsync(friendListInitCts.Token).Forget();
        }

        private void Disable()
        {
            panelLifecycleTask?.TrySetResult();
            friendListInitCts = friendListInitCts.SafeRestart();
        }

        private void RefreshLoopList()
        {
            view.LoopList.SetListItemCount(requestManager.GetCollectionCount(), false);
            view.LoopList.RefreshAllShownItem();
        }

        protected virtual async UniTaskVoid InitAsync(CancellationToken ct)
        {
            view.SetLoadingState(true);
            view.SetEmptyState(false);
            view.SetScrollViewState(false);

            await requestManager.InitAsync(ct);

            view.SetLoadingState(false);
            view.SetEmptyState(!requestManager.HasElements);
            view.SetScrollViewState(requestManager.HasElements);

            if (requestManager.HasElements)
                RefreshLoopList();
        }
    }
}
