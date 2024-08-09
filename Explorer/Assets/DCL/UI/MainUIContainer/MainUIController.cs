using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Minimap;
using DCL.SidebarBus;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.Sidebar;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.MainUI
{
    public class MainUIController.cs : ControllerBase<MainUIView>
    {
        private const int MS_WAIT_BEFORE_FIRST_HIDE = 3000;
        private const float SHOW_SIDEBAR_LAYOUT_WIDTH = 46;
        private const float HIDE_SIDEBAR_LAYOUT_WIDTH = 0;
        private const float HIDE_SIDEBAR_WAIT_TIME = 0.3f;
        private const float SHOW_SIDEBAR_WAIT_TIME = 0.3f;
        private const float SIDEBAR_ANIMATION_TIME = 0.2f;

        private readonly ISidebarBus sidebarBus;
        private readonly IMVCManager mvcManager;

        private bool waitingToShowSidebar;
        private bool waitingToHideSidebar;
        private bool showingSidebar;
        private bool sidebarBlockStatus;
        private bool autoHideSidebar = false;
        private CancellationTokenSource showSidebarCancellationTokenSource = new ();
        private CancellationTokenSource hideSidebarCancellationTokenSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public MainUIController(
            ViewFactoryMethod viewFactory,
            ISidebarBus sidebarBus,
            IMVCManager mvcManager) : base(viewFactory)
        {
            this.sidebarBus = sidebarBus;
            this.mvcManager = mvcManager;
        }

        protected override void OnViewInstantiated()
        {
            sidebarBus.SidebarBlockStatusChange += OnSidebarBlockStatusChanged;
            sidebarBus.SidebarAutohideStatusChange += OnSidebarAutohideStatusChanged;
            viewInstance.pointerDetectionArea.OnEnterArea += OnPointerEnter;
            viewInstance.pointerDetectionArea.OnExitArea += OnPointerExit;
            mvcManager.ShowAsync(SidebarController.IssueCommand()).Forget();
            mvcManager.ShowAsync(MinimapController.IssueCommand()).Forget();
            mvcManager.ShowAsync(ChatController.IssueCommand()).Forget();
            mvcManager.ShowAsync(ConnectionStatusPanelController.IssueCommand()).Forget();
            showingSidebar = true;
        }

        private void OnSidebarAutohideStatusChanged(bool status)
        {
            autoHideSidebar = status;
            hideSidebarCancellationTokenSource = hideSidebarCancellationTokenSource.SafeRestart();
            showSidebarCancellationTokenSource = showSidebarCancellationTokenSource.SafeRestart();
        }

        private void OnSidebarBlockStatusChanged(bool status)
        {
            sidebarBlockStatus = status;
            hideSidebarCancellationTokenSource = hideSidebarCancellationTokenSource.SafeRestart();
            showSidebarCancellationTokenSource = showSidebarCancellationTokenSource.SafeRestart();
        }

        private void OnPointerEnter()
        {
            if (!autoHideSidebar || sidebarBlockStatus) return;

            if (showingSidebar) { waitingToShowSidebar = false; }

            if (showSidebarCancellationTokenSource.IsCancellationRequested) { waitingToShowSidebar = false; }

            if (!showingSidebar && !waitingToShowSidebar)
            {
                waitingToShowSidebar = true;
                showSidebarCancellationTokenSource = showSidebarCancellationTokenSource.SafeRestart();
                WaitAndShowAsync(showSidebarCancellationTokenSource.Token).Forget();
            }

            if (waitingToHideSidebar) { hideSidebarCancellationTokenSource.Cancel(); }
        }

        private void OnPointerExit()
        {
            if (!autoHideSidebar || sidebarBlockStatus) return;

            if (waitingToShowSidebar || showingSidebar) { showSidebarCancellationTokenSource.Cancel(); }

            if (hideSidebarCancellationTokenSource.IsCancellationRequested) { waitingToHideSidebar = false; }

            if (!waitingToHideSidebar && showingSidebar)
            {
                waitingToHideSidebar = true;
                hideSidebarCancellationTokenSource = hideSidebarCancellationTokenSource.SafeRestart();
                WaitAndHideAsync(hideSidebarCancellationTokenSource.Token).Forget();
            }
        }


        private async UniTaskVoid WaitAndHideAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(HIDE_SIDEBAR_WAIT_TIME), cancellationToken: ct);
            waitingToHideSidebar = false;
            if (ct.IsCancellationRequested) return;

            await AnimateWidthAsync(HIDE_SIDEBAR_LAYOUT_WIDTH, ct);

            if (ct.IsCancellationRequested) return;
            showingSidebar = false;
            viewInstance.sidebarDetectionArea.SetActive(true);
        }

        private async UniTaskVoid WaitAndShowAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(SHOW_SIDEBAR_WAIT_TIME), cancellationToken: ct);
            waitingToShowSidebar = false;
            if (ct.IsCancellationRequested) return;

            await AnimateWidthAsync(SHOW_SIDEBAR_LAYOUT_WIDTH, ct);

            if (ct.IsCancellationRequested) return;
            showingSidebar = true;
            viewInstance.sidebarDetectionArea.SetActive(false);
        }

        private async UniTask AnimateWidthAsync(float width, CancellationToken ct) =>
            await viewInstance.sidebarLayoutElement.DOPreferredSize(new Vector2(width, 1f), SIDEBAR_ANIMATION_TIME).ToUniTask(cancellationToken: ct);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
