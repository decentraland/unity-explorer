using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using DCL.UI.SharedSpaceManager;

namespace DCL.UI.Controls
{
    public class ControlsPanelController : ControllerBase<ControlsPanelView>, IControllerInSharedSpace<ControlsPanelView>
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
            closeViewTask?.TrySetCanceled(ct);
            closeViewTask = new UniTaskCompletionSource();
            
            ViewShowingComplete?.Invoke(this);

            await closeViewTask.Task;
        }
        
        private void Close() =>
            closeViewTask?.TrySetResult();
        
        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            Close();
            
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }
    }
}
