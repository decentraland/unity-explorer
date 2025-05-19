using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Utilities;
using DCL.Utilities.Extensions;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;
using Utility.Types;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelSectionControllerBase<T, U> : IDisposable
        where T: FriendPanelSectionView
        where U: FriendPanelRequestManagerBase
    {
        protected readonly T view;
        protected readonly U requestManager;

        private CancellationTokenSource friendListInitCts = new ();

        protected UniTaskCompletionSource? panelLifecycleTask { get; private set; }

        protected FriendPanelSectionControllerBase(T view, U requestManager)
        {
            this.view = view;
            this.requestManager = requestManager;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            this.view.LoopList.InitListView(0, OnGetItemByIndex);

            this.view.LoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public virtual void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            requestManager.Dispose();
            friendListInitCts.SafeCancelAndDispose();
        }

        public virtual void Reset() =>
            requestManager.Reset();

        protected void CheckShouldInit()
        {
            if (!requestManager.WasInitialised)
                InitAsync(friendListInitCts.Token).Forget();
        }

        protected abstract void RefreshLoopList();

        protected abstract LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index);

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

        public async UniTask InitAsync(CancellationToken ct)
        {
            view.SetLoadingState(true);
            view.SetEmptyState(false);
            view.SetScrollViewState(false);

            EnumResult<TaskError> result = await requestManager.InitAsync(ct).SuppressToResultAsync(ReportCategory.FRIENDS);

            if (!result.Success)
                return;

            view.SetLoadingState(false);
            view.SetEmptyState(!requestManager.HasElements);
            view.SetScrollViewState(requestManager.HasElements);

            if (requestManager.HasElements)
                RefreshLoopList();
        }
    }
}
