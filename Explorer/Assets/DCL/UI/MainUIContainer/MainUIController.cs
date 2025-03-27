using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.PushNotifications;
using DCL.Minimap;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.SharedSpaceManager;
using DCL.UI.Sidebar;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.MainUI
{
    public class MainUIController : ControllerBase<MainUIView>
    {
        private const float SHOW_SIDEBAR_LAYOUT_WIDTH = 46;
        private const float HIDE_SIDEBAR_LAYOUT_WIDTH = 0;
        private const float HIDE_SIDEBAR_WAIT_TIME = 0.3f;
        private const float SHOW_SIDEBAR_WAIT_TIME = 0.3f;
        private const float SIDEBAR_ANIMATION_TIME = 0.2f;

        private readonly IMVCManager mvcManager;
        private readonly bool isFriendsEnabled;
        private readonly ISharedSpaceManager sharedSpaceManager;

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
            IMVCManager mvcManager,
            bool isFriendsEnabled,
            ISharedSpaceManager sharedSpaceManager) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.isFriendsEnabled = isFriendsEnabled;
            this.sharedSpaceManager = sharedSpaceManager;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.SidebarView.BlockStatusChanged += OnSidebarBlockStatusChanged;
            viewInstance.SidebarView.AutohideStatusChanged += OnSidebarAutohideStatusChanged;
            viewInstance!.pointerDetectionArea.OnEnterArea += OnPointerEnter;
            viewInstance.pointerDetectionArea.OnExitArea += OnPointerExit;
            mvcManager.ShowAsync(SidebarController.IssueCommand()).Forget();
            mvcManager.ShowAsync(MinimapController.IssueCommand()).Forget();
            sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatController.ShowParams(true)).Forget();
            mvcManager.ShowAsync(ConnectionStatusPanelController.IssueCommand()).Forget();

            if (isFriendsEnabled)
            {
                mvcManager.ShowAsync(FriendPushNotificationController.IssueCommand()).Forget();
                mvcManager.ShowAsync(PersistentFriendPanelOpenerController.IssueCommand()).Forget();
            }

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
