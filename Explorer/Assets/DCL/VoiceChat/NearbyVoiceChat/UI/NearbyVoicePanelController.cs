using Cysharp.Threading.Tasks;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using Utility;

namespace DCL.VoiceChat.Proximity
{
    public class NearbyVoicePanelController : ControllerBase<NearbyVoiceWidgetView>, IControllerInSharedSpace<NearbyVoiceWidgetView>
    {
        private CancellationTokenSource panelCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public NearbyVoicePanelController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        public override void Dispose()
        {
            base.Dispose();
            panelCts.SafeCancelAndDispose();

            if (!viewInstance) return;
            viewInstance.CloseAreaButton.onClick.RemoveAllListeners();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.CloseAreaButton.onClick.AddListener(OnClose);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            panelCts = panelCts.SafeRestart();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntilCanceled(panelCts.Token);
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            panelCts.Cancel();

            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        private void OnClose()
        {
            panelCts.Cancel();
        }
    }
}
