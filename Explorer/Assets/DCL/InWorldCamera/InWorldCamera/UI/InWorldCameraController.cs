using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.ExplorePanel;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.Playground;
using DCL.UI;
using DCL.UI.SharedSpaceManager;
using ECS.Abstract;
using MVC;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using CancellationTokenSource = System.Threading.CancellationTokenSource;

namespace DCL.InWorldCamera.UI
{
    /// <summary>
    ///     Handles Logic for the InWorldCamera HUD that appears when user enables InWorldCamera.
    /// </summary>
    public class InWorldCameraController : ControllerBase<InWorldCameraView>
    {
        private const string SOURCE_BUTTON = "Button";

        private readonly Button sidebarButton;
        private readonly World world;
        private readonly IMVCManager mvcManager;
        private readonly ICameraReelStorageService storageService;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private SingleInstanceEntity? cameraInternal;

        private bool shortcutPanelIsOpen;

        private CancellationTokenSource ctx;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;
        private SingleInstanceEntity? camera => cameraInternal ??= world.CacheCamera();

        public bool IsVfxInProgress => viewInstance != null && viewInstance.IsVfxInProgress;

        private UniTaskCompletionSource? closeViewTask;

        public InWorldCameraController(ViewFactoryMethod viewFactory, Button sidebarButton, World world, IMVCManager mvcManager, ICameraReelStorageService storageService, ISharedSpaceManager sharedSpaceManager) : base(viewFactory)
        {
            this.world = world;
            this.mvcManager = mvcManager;
            this.storageService = storageService;
            this.sidebarButton = sidebarButton;
            this.sharedSpaceManager = sharedSpaceManager;

            ctx = new CancellationTokenSource();
            closeViewTask = new UniTaskCompletionSource();

            storageService.ScreenshotUploaded += OnScreenshotUploaded;
            sidebarButton.onClick.AddListener(ToggleInWorldCamera);
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.CloseButton.onClick.AddListener(RequestDisableInWorldCamera);
            viewInstance.TakeScreenshotButton.onClick.AddListener(RequestTakeScreenshot);
            viewInstance.CameraReelButton.onClick.AddListener(OpenCameraReelGalleryAsync);
            viewInstance.ShortcutsInfoButton.onClick.AddListener(ToggleShortcutsInfo);
        }

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                viewInstance.CloseButton.onClick.RemoveListener(RequestDisableInWorldCamera);
                viewInstance.TakeScreenshotButton.onClick.RemoveListener(RequestTakeScreenshot);
                viewInstance.CameraReelButton.onClick.RemoveListener(OpenCameraReelGalleryAsync);
                viewInstance.ShortcutsInfoButton.onClick.RemoveListener(ToggleShortcutsInfo);
            }

            storageService.ScreenshotUploaded -= OnScreenshotUploaded;
            sidebarButton.onClick.RemoveListener(ToggleInWorldCamera);

            base.Dispose();
        }

        public void ToggleVisibility()
        {
            ctx = ctx.SafeRestart();

            if (viewInstance!.isActiveAndEnabled)
                viewInstance?.HideAsync(ctx.Token).Forget();
            else
                viewInstance?.ShowAsync(ctx.Token).Forget();
        }

        public void Show()
        {
            sidebarButton.OnSelect(null);
            mvcManager.ShowAsync(IssueCommand(new ControllerNoData()));

            AdjustToStorageSpace(storageService.StorageStatus.HasFreeSpace);
        }

        private void AdjustToStorageSpace(bool hasSpace)
        {
            viewInstance?.TakeScreenshotButton.gameObject.SetActive(hasSpace);
            viewInstance?.NoStorageNotification.gameObject.SetActive(!hasSpace);
        }

        public void Hide(bool isInstant = false)
        {
            ToggleShortcutsInfoAsync(toOpen: false);

            sidebarButton.OnDeselect(null);
            viewInstance?.HideAsync(default(CancellationToken), isInstant).Forget();
        }

        public void Close()
        {
            // Effectively hides the controller in the MVC system (otherwise it waits forever in WaitForCloseIntentAsync when it has not been closed using the UI buttons)
            closeViewTask?.TrySetResult();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask?.TrySetCanceled(ct);
            closeViewTask = new UniTaskCompletionSource();

            return closeViewTask.Task;
        }

        public void PlayScreenshotFX(Texture2D image, float splashDuration, float middlePauseDuration, float transitionDuration)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.SFXScreenshotCapture);
            viewInstance?.ScreenshotCaptureAnimation(image, splashDuration, middlePauseDuration, transitionDuration);
        }

        private async void OpenCameraReelGalleryAsync()
        {
            RequestDisableInWorldCamera();

            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden);

            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.CameraReel, BackpackSections.Avatar));
        }

        private void RequestTakeScreenshot()
        {
            if (!world.Has<TakeScreenshotRequest>(camera!.Value))
                world.Add(camera!.Value, new TakeScreenshotRequest { Source = SOURCE_BUTTON });
        }

        private void RequestDisableInWorldCamera()
        {
            if (world.Get<CameraComponent>(camera!.Value).CameraInputChangeEnabled && !world.Has<ToggleInWorldCameraRequest>(camera!.Value))
                world.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = false });
        }

        private void ToggleInWorldCamera()
        {
            if (world.Get<CameraComponent>(camera!.Value).CameraInputChangeEnabled && !world.Has<ToggleInWorldCameraRequest>(camera!.Value))
                world.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = !world.Has<InWorldCameraComponent>(camera!.Value), Source = SOURCE_BUTTON });
        }

        private void ToggleShortcutsInfo() =>
            ToggleShortcutsInfoAsync(!shortcutPanelIsOpen);

        private void ToggleShortcutsInfoAsync(bool toOpen)
        {
            if (toOpen)
            {
                viewInstance!.ShortcutsInfoPanel.ShowAsync(CancellationToken.None).Forget();
                viewInstance.ShortcutsInfoPanel.Closed += OnShortcutsInfoPanelClosed;
                viewInstance.ShortcutsInfoButton.OnSelect(null);
                shortcutPanelIsOpen = true;
            }
            else
            {
                if (viewInstance != null)
                {
                    viewInstance.ShortcutsInfoPanel.Closed -= OnShortcutsInfoPanelClosed;
                    viewInstance.ShortcutsInfoPanel.HideAsync(CancellationToken.None).Forget();
                    viewInstance.ShortcutsInfoButton.OnDeselect(null);
                }

                shortcutPanelIsOpen = false;
            }
        }

        private void OnShortcutsInfoPanelClosed()
        {
            ToggleShortcutsInfoAsync(false);
        }

        private void OnScreenshotUploaded(CameraReelResponse _, CameraReelStorageStatus storage, string __) =>
            AdjustToStorageSpace(storage.HasFreeSpace);

        [Conditional("DEBUG")]
        public void DebugCapture(Texture2D screenshot, ScreenshotMetadata metadata)
        {
            if (!viewInstance!.gameObject.TryGetComponent(out ScreenshotHudDebug hud))
                hud = viewInstance.gameObject.AddComponent<ScreenshotHudDebug>();

            hud.Screenshot = screenshot;
            hud.Metadata = metadata;
        }
    }
}
