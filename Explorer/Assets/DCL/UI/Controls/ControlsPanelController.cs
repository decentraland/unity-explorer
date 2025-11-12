using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using DCL.UI.SharedSpaceManager;

namespace DCL.UI.Controls
{
    public class ControlsPanelController : ControllerBase<ControlsPanelView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        private bool closePanel;
        private UniTaskCompletionSource? closeViewTask;

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public ControlsPanelController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.closeButton.onClick.AddListener(Close);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask = new UniTaskCompletionSource();

            // Handle external cancellation
            using var registration = ct.Register(() => closeViewTask.TrySetCanceled(ct));

            await closeViewTask.Task;

            closeViewTask?.TrySetCanceled(ct);
            closeViewTask = new UniTaskCompletionSource();
            await closeViewTask.Task;
        }

        private void Close() => closeViewTask?.TrySetResult();
    }
}
