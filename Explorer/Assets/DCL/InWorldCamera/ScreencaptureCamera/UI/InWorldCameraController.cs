using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.ExplorePanel;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.UI;
using ECS.Abstract;
using JetBrains.Annotations;
using MVC;
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

        private SingleInstanceEntity? cameraInternal;

        private bool shortcutPanelIsOpen;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;
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

            LaunchViewLifeCycleAsync(new CanvasOrdering(Layer, 0), new ControllerNoData(), default(CancellationToken))
               .Forget();

            viewInstance!.TakeScreenshotButton.interactable = storageService.StorageStatus.HasFreeSpace;
            viewInstance.NoStorageNotification.gameObject.SetActive(!storageService.StorageStatus.HasFreeSpace);
        }

        public void Hide()
        {
            sidebarButton.OnDeselect(null);
            viewInstance.HideAsync(default(CancellationToken)).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance.CameraReelButton.OnClickAsync(ct));

        public void PlayScreenshotFX(Texture2D image, float splashDuration, float middlePauseDuration, float transitionDuration)
        {
            // AudioScriptableObjects.takeScreenshot.Play();
            viewInstance.ScreenshotCaptureAnimation(image, splashDuration, middlePauseDuration, transitionDuration);
        }

        private void OpenCameraReelGallery()
        {
            RequestDisableInWorldCamera();

            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(ExploreSections.CameraReel, BackpackSections.Avatar)));
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

        private void ToggleShortcutsInfo()
        {
            if (shortcutPanelIsOpen)
            {
                viewInstance!.ShortcutsInfoPanel.HideAsync(CancellationToken.None).Forget();
                viewInstance.ShortcutsInfoButton.OnDeselect(null);
                shortcutPanelIsOpen = false;
            }
            else
            {
                viewInstance!.ShortcutsInfoPanel.ShowAsync(CancellationToken.None).Forget();
                viewInstance.ShortcutsInfoButton.OnSelect(null);
                shortcutPanelIsOpen = true;
            }
        }
    }
}
