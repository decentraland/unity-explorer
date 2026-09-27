using Cysharp.Threading.Tasks;
using MVC;
using Runtime.Wearables;
using System.Threading;
using Utility;
using Utility.PortableExperiences;

namespace DCL.UI.Skybox
{
    public class PortableExperiencesSideBarTooltipController : ControllerBase<PortableExperiencesSideBarTooltipView>
    {
        private readonly SmartWearableCache smartWearableCache;
        private readonly ILocalPortableExperiencesStatus localPortableExperiencesStatus;
        private readonly IPortableExperiencesStatus globalPortableExperiencesStatus;

        private CancellationTokenSource? cancellationTokenSource;

        public PortableExperiencesSideBarTooltipController(ViewFactoryMethod viewFactory,
            SmartWearableCache smartWearableCache,
            ILocalPortableExperiencesStatus localPortableExperiencesStatus,
            IPortableExperiencesStatus globalPortableExperiencesStatus) : base(viewFactory)
        {
            this.smartWearableCache = smartWearableCache;
            this.localPortableExperiencesStatus = localPortableExperiencesStatus;
            this.globalPortableExperiencesStatus = globalPortableExperiencesStatus;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestartLinked(ct);
            await UniTask.WaitUntilCanceled(cancellationTokenSource.Token);
        }

        protected override void OnBeforeViewShow()
        {
            SetupView();
            base.OnBeforeViewShow();
        }

        private void SetupView()
        {
            viewInstance?.Setup(
                smartWearableCache.AuthorizedSmartWearables.Count,
                smartWearableCache.RunningSmartWearables.Count,
                smartWearableCache.KilledPortableExperiences.Count,
                globalPortableExperiencesStatus.RunningPortableExperiences.Count,
                localPortableExperiencesStatus.AuthorizedPortableExperiences.Count,
                localPortableExperiencesStatus.RunningPortableExperiences.Count,
                localPortableExperiencesStatus.KilledPortableExperiences.Count);
        }

        public void Close()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }
    }
}
