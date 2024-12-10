using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.ExplorePanel;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.Playground;
using DCL.UI;
using ECS.Abstract;
using MVC;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.UI
{
    /// <summary>
    /// Handles Logic for the InWorldCamera HUD that appears when user enables InWorldCamera.
    /// </summary>
    public class InWorldCameraController : ControllerBase<InWorldCameraView>
    {
        private readonly Button sidebarButton;
        private readonly World world;
        private readonly IMVCManager mvcManager;
        private readonly ICameraReelStorageService storageService;

        private ScreencaptureShortcutsController shortcutsController;
        private SingleInstanceEntity? cameraInternal;

        private bool shortcutPanelIsOpen;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;
        private SingleInstanceEntity? camera => cameraInternal ??= world.CacheCamera();

        public InWorldCameraController(ViewFactoryMethod viewFactory, Button sidebarButton, World world, IMVCManager mvcManager, ICameraReelStorageService storageService) : base(viewFactory)
        {
            this.world = world;
            this.mvcManager = mvcManager;
            this.storageService = storageService;
            this.sidebarButton = sidebarButton;

            sidebarButton.onClick.AddListener(ToggleInWorldCamera);
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.CloseButton.onClick.AddListener(RequestDisableInWorldCamera);
            viewInstance.TakeScreenshotButton.onClick.AddListener(RequestTakeScreenshot);
            viewInstance.CameraReelButton.onClick.AddListener(OpenCameraReelGallery);
            viewInstance.ShortcutsInfoButton.onClick.AddListener(ToggleShortcutsInfo);

            shortcutsController = new ScreencaptureShortcutsController(() => viewInstance.ShortcutsInfoPanel);
            mvcManager.RegisterController(shortcutsController);
        }

        public override void Dispose()
        {
            viewInstance!.CloseButton.onClick.RemoveListener(RequestDisableInWorldCamera);
            viewInstance.TakeScreenshotButton.onClick.RemoveListener(RequestTakeScreenshot);
            viewInstance.CameraReelButton.onClick.RemoveListener(OpenCameraReelGallery);
            viewInstance.ShortcutsInfoButton.onClick.RemoveListener(ToggleShortcutsInfo);

            sidebarButton.onClick.RemoveListener(ToggleInWorldCamera);

            base.Dispose();
        }

        public void Show()
        {
            sidebarButton.OnSelect(null);
            mvcManager.ShowAsync(IssueCommand(new ControllerNoData()));

            bool hasSpace = storageService.StorageStatus.HasFreeSpace;
            viewInstance?.TakeScreenshotButton.gameObject.SetActive(hasSpace);
            viewInstance?.NoStorageNotification.gameObject.SetActive(!hasSpace);
        }

        public void Hide(bool isInstant = false)
        {
            sidebarButton.OnDeselect(null);
            viewInstance?.HideAsync(default(CancellationToken), isInstant).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance.CameraReelButton.OnClickAsync(ct));

        public void PlayScreenshotFX(Texture2D image, float splashDuration, float middlePauseDuration, float transitionDuration)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.SFXScreenshotCapture);
            viewInstance?.ScreenshotCaptureAnimation(image, splashDuration, middlePauseDuration, transitionDuration);
        }

        private void OpenCameraReelGallery()
        {
            RequestDisableInWorldCamera();

            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.CameraReel, BackpackSections.Avatar)));
        }

        private void RequestTakeScreenshot()
        {
            if (!world.Has<TakeScreenshotRequest>(camera!.Value))
                world.Add(camera!.Value, new TakeScreenshotRequest { Source = "UI" });
        }

        private void RequestDisableInWorldCamera()
        {
            if (!world.Has<ToggleInWorldCameraRequest>(camera!.Value))
                world.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = false });
        }

        private void ToggleInWorldCamera()
        {
            if (!world.Has<ToggleInWorldCameraRequest>(camera!.Value))
                world.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = !world.Has<InWorldCameraComponent>(camera!.Value) });
        }

        private void ToggleShortcutsInfo() =>
            ToggleShortcutsInfo(!shortcutPanelIsOpen);

        private void ToggleShortcutsInfo(bool toOpen)
        {
            if (toOpen)
            {
                shortcutsController.LaunchViewLifeCycleAsync(new CanvasOrdering(shortcutsController.Layer, 0), new ControllerNoData(), default(CancellationToken))
                                   .Forget();

                viewInstance?.ShortcutsInfoButton.OnSelect(null);
                shortcutPanelIsOpen = true;
            }
            else
            {
                shortcutsController.HideAsync(CancellationToken.None).Forget();
                viewInstance?.ShortcutsInfoButton.OnDeselect(null);
                shortcutPanelIsOpen = false;
            }
        }

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
