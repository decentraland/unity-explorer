using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using ECS.Abstract;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using UnityEngine.UI;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public class InWorldCameraController : ControllerBase<InWorldCameraView>
    {
        private readonly Button sidebarButton;
        private readonly World world;

        private SingleInstanceEntity? cameraInternal;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;
        private SingleInstanceEntity? camera => cameraInternal ??= world.CacheCamera();

        public InWorldCameraController([NotNull] ViewFactoryMethod viewFactory, Button sidebarButton, World world) : base(viewFactory)
        {
            this.world = world;
            this.sidebarButton = sidebarButton;

            sidebarButton.onClick.AddListener(RequestEnableInWorldCamera);
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.CloseButton.onClick.AddListener(RequestDisableInWorldCamera);

            // viewInstance!.CameraReelButton.onClick.AddListener(OpenCameraReelGallery);
            // viewInstance.TakeScreenshotButton.onClick.AddListener(TakeScreenshot);
            // viewInstance.ShortcutsInfoButton.onClick.AddListener(() => { });
        }

        public override void Dispose()
        {
            sidebarButton.onClick.RemoveListener(RequestEnableInWorldCamera);
            viewInstance.CloseButton.onClick.RemoveListener(RequestDisableInWorldCamera);

            base.Dispose();
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

        public void Show()
        {
            LaunchViewLifeCycleAsync(new CanvasOrdering(Layer, 0), new ControllerNoData(), default(CancellationToken))
               .Forget();
        }

        public void Hide()
        {
            viewInstance.HideAsync(default(CancellationToken)).Forget();
        }

        // protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
        //     UniTask.WhenAny(
        //         viewInstance!.CloseButton.OnClickAsync(ct),
        //         viewInstance.BackgroundButton.OnClickAsync(ct));
        //

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
