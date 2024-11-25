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


        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private SingleInstanceEntity? cameraInternal;
        private SingleInstanceEntity? camera => cameraInternal ??= world.CacheCamera();

        public InWorldCameraController([NotNull] ViewFactoryMethod viewFactory, Button sidebarButton, World world) : base(viewFactory)
        {
            this.world = world;
            this.sidebarButton = sidebarButton;

            sidebarButton.onClick.AddListener(RequestInWorldCamera);
        }

        public override void Dispose()
        {
            sidebarButton.onClick.RemoveListener(RequestInWorldCamera);
            base.Dispose();
        }

        private void RequestInWorldCamera()
        {
            if (CameraIsNotActive())
                world.Add<EnableInWorldCameraUIRequest>(camera!.Value);

            bool CameraIsNotActive() =>
                !world.Has<InWorldCamera>(camera!.Value) && !world.Has<EnableInWorldCameraUIRequest>(camera!.Value);
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
