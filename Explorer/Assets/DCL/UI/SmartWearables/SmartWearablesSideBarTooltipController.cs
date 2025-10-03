using Cysharp.Threading.Tasks;
using DCL.UI.SharedSpaceManager;
using MVC;
using Runtime.Wearables;
using System.Threading;
using Utility;

namespace DCL.UI.Skybox
{
    public class SmartWearablesSideBarTooltipController : ControllerBase<SmartWearablesSideBarTooltipView>, IControllerInSharedSpace<SmartWearablesSideBarTooltipView>
    {
        private readonly SmartWearableCache smartWearableCache;

        private CancellationTokenSource? cancellationTokenSource;

        public SmartWearablesSideBarTooltipController(ViewFactoryMethod viewFactory, SmartWearableCache smartWearableCache) : base(viewFactory)
        {
            this.smartWearableCache = smartWearableCache;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            SetupView();

            ViewShowingComplete?.Invoke(this);
            cancellationTokenSource = new CancellationTokenSource();
            await UniTask.WaitUntilCanceled(cancellationTokenSource.Token);
        }

        private void SetupView()
        {
            bool smartWearablesAllowed = smartWearableCache.CurrentSceneAllowsSmartWearables;
            int smartWearableCount = smartWearableCache.RunningSmartWearables.Count;
            viewInstance?.Setup(smartWearablesAllowed, smartWearableCount);
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
