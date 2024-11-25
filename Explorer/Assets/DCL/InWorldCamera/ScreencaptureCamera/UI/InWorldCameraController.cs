using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using UnityEngine.UI;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public class InWorldCameraController : ControllerBase<InWorldCameraView>
    {
        private readonly Button sidebarButton;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public InWorldCameraController([NotNull] ViewFactoryMethod viewFactory, Button sidebarButton) : base(viewFactory)
        {
            this.sidebarButton = sidebarButton;
            sidebarButton.onClick.AddListener(ActivateInWorldCamera);
        }

        public override void Dispose()
        {
            sidebarButton.onClick.RemoveListener(ActivateInWorldCamera);
        }

        private void ActivateInWorldCamera()
        {
            LaunchViewLifeCycleAsync(new CanvasOrdering(Layer, 0), new ControllerNoData(), default(CancellationToken))
               .Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
