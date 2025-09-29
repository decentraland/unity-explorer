using Cysharp.Threading.Tasks;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Skybox
{
    public class SmartWearablesSideBarTooltipController : ControllerBase<SmartWearablesSideBarTooltipView>, IControllerInSharedSpace<SmartWearablesSideBarTooltipView>
    {
        private CancellationTokenSource? cancellationTokenSource;

        public SmartWearablesSideBarTooltipController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            cancellationTokenSource = new CancellationTokenSource();
            await UniTask.WaitUntilCanceled(cancellationTokenSource.Token);
        }

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            Close();
            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        public void Close()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }
    }
}
