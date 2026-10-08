using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SingleScene;
using MVC;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.MainUI
{
    /// <summary>
    ///     A toast on entering the world, and one on stepping outside the scene the restriction is anchored to.
    ///     Both dismiss themselves; walking back in hides the second one early.
    /// </summary>
    public class SingleSceneModeHudController : IDisposable
    {
        private const int TOAST_DURATION_MS = 10000;

        private readonly SingleSceneMode singleSceneMode;
        private readonly IScenesCache scenesCache;
        private readonly IMVCManager mvcManager;
        private readonly WarningNotificationView onToast;
        private readonly WarningNotificationView endOfSceneToast;
        private readonly Button openPlacesButton;
        private readonly CancellationTokenSource onToastCancellationTokenSource = new ();

        private CancellationTokenSource endOfSceneToastCancellationTokenSource = new ();
        private bool endOfSceneToastShown;

        public SingleSceneModeHudController(
            SingleSceneMode singleSceneMode,
            IScenesCache scenesCache,
            IMVCManager mvcManager,
            WarningNotificationView onToast,
            WarningNotificationView endOfSceneToast,
            Button openPlacesButton)
        {
            this.singleSceneMode = singleSceneMode;
            this.scenesCache = scenesCache;
            this.mvcManager = mvcManager;
            this.onToast = onToast;
            this.endOfSceneToast = endOfSceneToast;
            this.openPlacesButton = openPlacesButton;
        }

        public void Activate()
        {
            if (!singleSceneMode.IsActive)
                return;

            ShowTimedToastAsync(onToast, onToastCancellationTokenSource.Token).Forget();

            openPlacesButton.onClick.AddListener(OpenPlaces);

            scenesCache.CurrentParcel.OnUpdate += OnCurrentParcelChanged;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;

            RefreshEndOfSceneToast();
        }

        public void Dispose()
        {
            onToastCancellationTokenSource.SafeCancelAndDispose();
            endOfSceneToastCancellationTokenSource.SafeCancelAndDispose();

            if (!singleSceneMode.IsActive)
                return;

            openPlacesButton.onClick.RemoveListener(OpenPlaces);
            scenesCache.CurrentParcel.OnUpdate -= OnCurrentParcelChanged;
            scenesCache.CurrentScene.OnUpdate -= OnCurrentSceneChanged;
        }

        private void OpenPlaces() =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Places)));

        private void OnCurrentParcelChanged(Vector2Int _) =>
            RefreshEndOfSceneToast();

        private void OnCurrentSceneChanged(ISceneFacade? _) =>
            RefreshEndOfSceneToast();

        private void RefreshEndOfSceneToast()
        {
            bool outside = singleSceneMode.IsRestricting
                           && scenesCache.TryGetByParcel(singleSceneMode.AnchorParcel, out ISceneFacade anchorScene)
                           && !ReferenceEquals(anchorScene, scenesCache.CurrentScene.Value);

            if (outside == endOfSceneToastShown)
                return;

            endOfSceneToastShown = outside;

            endOfSceneToastCancellationTokenSource = endOfSceneToastCancellationTokenSource.SafeRestart();

            if (outside)
                ShowTimedToastAsync(endOfSceneToast, endOfSceneToastCancellationTokenSource.Token).Forget();
            else
                endOfSceneToast.Hide(ct: endOfSceneToastCancellationTokenSource.Token);
        }

        private async UniTaskVoid ShowTimedToastAsync(WarningNotificationView toast, CancellationToken ct)
        {
            try { await toast.AnimatedShowAsync(TOAST_DURATION_MS, ct, toggleGameObject: true); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.UI)); }
        }
    }
}
