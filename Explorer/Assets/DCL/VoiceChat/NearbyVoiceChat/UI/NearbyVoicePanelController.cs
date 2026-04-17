using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.VoiceChat.UI
{
    public class NearbyVoicePanelController : ControllerBase<NearbyVoiceWidgetView>
    {
        private UniTaskCompletionSource? closeViewTask;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public NearbyVoicePanelController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.CloseAreaButton.onClick.AddListener(OnClose);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask = new UniTaskCompletionSource();
            await closeViewTask.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        protected override void OnViewClose()
        {
            closeViewTask?.TrySetResult();
            viewInstance?.CloseAreaButton.onClick.RemoveListener(OnClose);
        }

        private void OnClose() =>
            closeViewTask?.TrySetResult();
    }
}
