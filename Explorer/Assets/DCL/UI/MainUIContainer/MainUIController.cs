using Cysharp.Threading.Tasks;
using DCL.ExplorePanel;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.PushNotifications;
using DCL.Minimap;
using DCL.UI.Sidebar;
using DG.Tweening;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SingleScene;
using SceneRunner.Scene;
using MVC;
using DCL.Diagnostics;
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
        private const int PERFORMANCE_MODE_TOAST_DURATION_MS = 10000;

        private readonly IMVCManager mvcManager;
        private readonly bool isFriendsEnabled;
        private readonly SingleSceneMode singleSceneMode;
        private readonly IScenesCache scenesCache;

        private bool endOfSceneToastShown;

        private bool waitingToShowSidebar;
        private bool waitingToHideSidebar;
        private bool showingSidebar;
        private bool sidebarBlockStatus;
        private bool autoHideSidebar = false;
        private CancellationTokenSource showSidebarCancellationTokenSource = new ();
        private CancellationTokenSource hideSidebarCancellationTokenSource = new ();
        private readonly CancellationTokenSource performanceModeToastCancellationTokenSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public MainUIController(
            ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            bool isFriendsEnabled,
            SingleSceneMode singleSceneMode,
            IScenesCache scenesCache) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.isFriendsEnabled = isFriendsEnabled;
            this.singleSceneMode = singleSceneMode;
            this.scenesCache = scenesCache;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.SidebarView.BlockStatusChanged += OnSidebarBlockStatusChanged;
            viewInstance.SidebarView.AutohideStatusChanged += OnSidebarAutohideStatusChanged;
            viewInstance!.pointerDetectionArea.OnEnterArea += OnPointerEnter;
            viewInstance.pointerDetectionArea.OnExitArea += OnPointerExit;
            mvcManager.ShowAsync(SidebarController.IssueCommand()).Forget();
            mvcManager.ShowAsync(MinimapController.IssueCommand()).Forget();

            if (isFriendsEnabled)
            {
                mvcManager.ShowAsync(FriendPushNotificationController.IssueCommand()).Forget();
                mvcManager.ShowAsync(PersistentFriendPanelOpenerController.IssueCommand()).Forget();
            }

            showingSidebar = true;

            if (!singleSceneMode.IsActive)
                return;

            ShowPerformanceModeToastAsync(performanceModeToastCancellationTokenSource.Token).Forget();

            viewInstance.EndOfSceneOpenPlacesButton.onClick.AddListener(OpenPlaces);

            // The parcel covers walking out, the scene covers the anchored scene finishing its load under a player
            // who has not moved since the teleport
            scenesCache.CurrentParcel.OnUpdate += OnCurrentParcelChanged;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;

            RefreshEndOfSceneToast();
        }

        public override void Dispose()
        {
            performanceModeToastCancellationTokenSource.SafeCancelAndDispose();

            if (!singleSceneMode.IsActive)
                return;

            scenesCache.CurrentParcel.OnUpdate -= OnCurrentParcelChanged;
            scenesCache.CurrentScene.OnUpdate -= OnCurrentSceneChanged;
        }

        private void OpenPlaces() =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Places)));

        private void OnCurrentParcelChanged(Vector2Int _) =>
            RefreshEndOfSceneToast();

        private void OnCurrentSceneChanged(ISceneFacade? _) =>
            RefreshEndOfSceneToast();

        /// <summary>
        ///     Only the anchored scene is ever loaded while the restriction is on, so "standing on the anchored scene"
        ///     is the same question as "standing on the one scene the cache holds". An anchor that is not in the cache
        ///     yet is still loading, which is not the same as having walked out of it.
        /// </summary>
        private void RefreshEndOfSceneToast()
        {
            bool outside = singleSceneMode.IsRestricting
                           && scenesCache.TryGetByParcel(singleSceneMode.AnchorParcel, out ISceneFacade anchorScene)
                           && (!scenesCache.TryGetByParcel(scenesCache.CurrentParcel.Value, out ISceneFacade currentScene)
                               || !ReferenceEquals(anchorScene, currentScene));

            if (outside == endOfSceneToastShown)
                return;

            endOfSceneToastShown = outside;

            if (outside)
                viewInstance!.EndOfSceneToast.Show(toggleGameObject: true);
            else
                viewInstance!.EndOfSceneToast.Hide();
        }

        private async UniTaskVoid ShowPerformanceModeToastAsync(CancellationToken ct)
        {
            try { await viewInstance!.PerformanceModeOnToast.AnimatedShowAsync(PERFORMANCE_MODE_TOAST_DURATION_MS, ct, toggleGameObject: true); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.UI)); }
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
