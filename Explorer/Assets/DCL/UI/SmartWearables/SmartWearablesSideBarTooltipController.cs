using Cysharp.Threading.Tasks;
using MVC;
using Runtime.Wearables;
using System.Threading;
using Utility;

namespace DCL.UI.Skybox
{
    public class SmartWearablesSideBarTooltipController : ControllerBase<SmartWearablesSideBarTooltipView>
    {
        private readonly SmartWearableCache smartWearableCache;

        private CancellationTokenSource? cancellationTokenSource;

        public SmartWearablesSideBarTooltipController(ViewFactoryMethod viewFactory, SmartWearableCache smartWearableCache) : base(viewFactory)
        {
            this.smartWearableCache = smartWearableCache;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            SetupView();
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await UniTask.WaitUntilCanceled(cancellationTokenSource.Token);
        }

        private void SetupView()
        {
            bool smartWearablesAllowed = smartWearableCache.CurrentSceneAllowsSmartWearables;
            int smartWearableCount = smartWearableCache.RunningSmartWearables.Count;
            int killedCount = smartWearableCache.KilledPortableExperiences.Count;
            viewInstance?.Setup(smartWearablesAllowed, smartWearableCount, killedCount);
        }

        public void Close()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }
    }
}
