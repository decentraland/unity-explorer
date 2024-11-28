using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.ExplorePanel;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.InWorldCamera.Playground;
using DCL.UI;
using ECS.Abstract;
using JetBrains.Annotations;
using MVC;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
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

        public InWorldCameraController([NotNull] ViewFactoryMethod viewFactory, Button sidebarButton, World world, IMVCManager mvcManager, ICameraReelStorageService storageService) : base(viewFactory)
        {
            this.world = world;
            this.mvcManager = mvcManager;
            this.storageService = storageService;
            this.sidebarButton = sidebarButton;

            sidebarButton.onClick.AddListener(RequestEnableInWorldCamera);
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

            base.Dispose();
        }

        public void Show()
        {
            sidebarButton.OnSelect(null);
            mvcManager.ShowAsync(InWorldCameraController.IssueCommand(new ControllerNoData()));

            bool hasSpace = storageService.StorageStatus.HasFreeSpace;
            viewInstance!.TakeScreenshotButton.gameObject.SetActive(hasSpace);
            viewInstance.NoStorageNotification.gameObject.SetActive(!hasSpace);
        }

        public void Hide(bool isInstant = false)
        {
            sidebarButton.OnDeselect(null);
            viewInstance.HideAsync(default(CancellationToken), isInstant).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance.CameraReelButton.OnClickAsync(ct));

        public void PlayScreenshotFX(Texture2D image, float splashDuration, float middlePauseDuration, float transitionDuration)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.SFXScreenshotCapture);
            viewInstance.ScreenshotCaptureAnimation(image, splashDuration, middlePauseDuration, transitionDuration);
        }

        private void OpenCameraReelGallery()
        {
            RequestDisableInWorldCamera();

            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.CameraReel, BackpackSections.Avatar)));
        }

        private void RequestTakeScreenshot()
        {
            if (!world.Has<TakeScreenshotUIRequest>(camera!.Value))
                world.Add(camera!.Value, new TakeScreenshotUIRequest { Source = "UI" });
        }

        private void RequestDisableInWorldCamera()
        {
            if (CameraIsActivated())
                world.Add(camera!.Value, new ToggleInWorldCameraUIRequest { IsEnable = false });

            bool CameraIsActivated() =>
                !world.Has<ToggleInWorldCameraUIRequest>(camera!.Value) && world.Has<InWorldCamera>(camera!.Value);
        }

        private void RequestEnableInWorldCamera()
        {
            if (CameraIsNotActivated())
                world.Add(camera!.Value, new ToggleInWorldCameraUIRequest { IsEnable = true });

            bool CameraIsNotActivated() =>
                !world.Has<ToggleInWorldCameraUIRequest>(camera!.Value) && !world.Has<InWorldCamera>(camera!.Value);
        }

        private void ToggleShortcutsInfo() =>
            ToggleShortcutsInfo(!shortcutPanelIsOpen);

        private void ToggleShortcutsInfo(bool toOpen)
        {
            if (toOpen)
            {
                shortcutsController.LaunchViewLifeCycleAsync(new CanvasOrdering(shortcutsController.Layer, 0), new ControllerNoData(), default(CancellationToken))
                   .Forget();
                viewInstance!.ShortcutsInfoButton.OnSelect(null);
                shortcutPanelIsOpen = true;
            }
            else
            {
                shortcutsController.HideAsync(CancellationToken.None).Forget();
                viewInstance!.ShortcutsInfoButton.OnDeselect(null);
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
