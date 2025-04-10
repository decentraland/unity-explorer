using Cysharp.Threading.Tasks;
using DCL.UI.Utilities;
using MVC;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine.UI;
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
        protected readonly IMVCManager mvcManager;
        protected readonly U requestManager;

        protected UniTaskCompletionSource? panelLifecycleTask;
        private CancellationTokenSource friendListInitCts = new ();

        protected FriendPanelSectionDoubleCollectionController(T view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IMVCManager mvcManager,
            U requestManager)
        {
            this.view = view;
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.mvcManager = mvcManager;
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
            requestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
            requestManager.FirstFolderClicked -= FolderClicked;
            requestManager.SecondFolderClicked -= FolderClicked;
            requestManager.ElementClicked -= ElementClicked;
        }

        public virtual void Reset()
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

        private void Enable()
        {
            view.LoopList.ResetListView();
            panelLifecycleTask = new UniTaskCompletionSource();
            CheckShouldInit();
        }

        private void Disable()
        {
            panelLifecycleTask?.TrySetResult();
            friendListInitCts = friendListInitCts.SafeRestart();
        }

        protected void FolderClicked()
        {
            RefreshLoopList();
        }

        protected void RefreshLoopList()
        {
            view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
            view.LoopList.RefreshAllShownItem();
        }

        public virtual async UniTask InitAsync(CancellationToken ct)
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
