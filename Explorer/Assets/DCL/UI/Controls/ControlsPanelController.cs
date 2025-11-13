using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.UI.Controls
{
    public class ControlsPanelController : ControllerBase<ControlsPanelView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        private bool closePanel;
        private UniTaskCompletionSource? closeViewTask;

        public ControlsPanelController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnViewInstantiated()
        {
            viewInstance!.closeButton.onClick.AddListener(Close);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask = new UniTaskCompletionSource();
            await closeViewTask.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        private void Close() => closeViewTask?.TrySetCanceled();
    }
}
