using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Minimap;
using DCL.SidebarBus;
using DCL.UI.Sidebar;
using JetBrains.Annotations;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.MainUI
{
    public class MainUIController : ControllerBase<MainUIView>
    {
        private const float SHOW_SIDEBAR_LAYOUT_WIDTH = 80;
        private const float HIDE_SIDEBAR_LAYOUT_WIDTH = 20;
        private const float HIDE_SIDEBAR_WAIT_TIME = 0.3f;
        private const float SHOW_SIDEBAR_WAIT_TIME = 0.3f;
        private const float SIDEBAR_ANIMATION_TIME = 0.2f;

        private readonly ISidebarBus sidebarBus;
        private readonly IMVCManager mvcManager;

        private bool waitingToShowSidebar;
        private bool waitingToHideSidebar;
        private bool showingSidebar;
        private bool sidebarBlockStatus;
        private bool autoHideSidebar = true;
        private CancellationTokenSource showSidebarCancellationTokenSource = new ();
        private CancellationTokenSource hideSidebarCancellationTokenSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public MainUIController(
            [NotNull] ViewFactoryMethod viewFactory,
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
                WaitAndShow(showSidebarCancellationTokenSource.Token).Forget();
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
                WaitAndHide(hideSidebarCancellationTokenSource.Token).Forget();
            }
        }

        private async UniTask WaitAndHide(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(HIDE_SIDEBAR_WAIT_TIME), cancellationToken: ct);
            waitingToHideSidebar = false;
            if (ct.IsCancellationRequested) return;

            await AnimateWidthAsync(HIDE_SIDEBAR_LAYOUT_WIDTH, ct);

            if (ct.IsCancellationRequested) return;
            showingSidebar = false;
        }

        private async UniTask WaitAndShow(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(SHOW_SIDEBAR_WAIT_TIME), cancellationToken: ct);
            waitingToShowSidebar = false;
            if (ct.IsCancellationRequested) return;

            await AnimateWidthAsync(SHOW_SIDEBAR_LAYOUT_WIDTH, ct);

            if (ct.IsCancellationRequested) return;
            showingSidebar = true;
        }

        private async UniTask AnimateWidthAsync(float width, CancellationToken ct)
        {
            float startWidth = viewInstance.sidebarLayoutElement.preferredWidth;
            float endWidth = width;
            var elapsedTime = 0f;

            while (elapsedTime < SIDEBAR_ANIMATION_TIME)
            {
                if (ct.IsCancellationRequested)
                {
                    viewInstance.sidebarLayoutElement.preferredWidth = startWidth;
                    return;
                }

                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / SIDEBAR_ANIMATION_TIME);
                viewInstance.sidebarLayoutElement.preferredWidth = Mathf.Lerp(startWidth, endWidth, t);
                await UniTask.Yield();
            }

            viewInstance.sidebarLayoutElement.preferredWidth = endWidth;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
