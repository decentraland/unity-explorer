using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Web3;
using ECS.SceneLifeCycle;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Donations.UI
{
    public class DonationsPanelController : ControllerBase<DonationsPanelView>
    {
        private readonly IEthereumApi ethereumApi;
        private readonly IScenesCache scenesCache;
        private readonly IProfileRepository profileRepository;

        private CancellationTokenSource panelLifecycleCts = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public DonationsPanelController(ViewFactoryMethod viewFactory,
            IEthereumApi ethereumApi,
            IScenesCache scenesCache,
            IProfileRepository profileRepository)
            : base(viewFactory)
        {
            this.ethereumApi = ethereumApi;
            this.scenesCache = scenesCache;
            this.profileRepository = profileRepository;
        }

        public override void Dispose()
        {
            panelLifecycleCts.SafeCancelAndDispose();
        }

        private void CloseController() =>
            closeIntentCompletionSource.TrySetResult();

        protected override void OnBeforeViewShow()
        {
            panelLifecycleCts = panelLifecycleCts.SafeRestart();
            closeIntentCompletionSource = new UniTaskCompletionSource();
            LoadDataAsync(panelLifecycleCts.Token).Forget();
        }

        private async UniTaskVoid LoadDataAsync(CancellationToken ct)
        {
            try
            {
                viewInstance!.SetLoadingState(true);
                profileRepository.GetAsync()
                // Simulate data loading
                await UniTask.Delay(2000);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.DONATIONS);
                CloseController();
            }
            finally
            {
                viewInstance!.SetLoadingState(false);
            }
        }

        protected override void OnViewClose()
        {
            panelLifecycleCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetClosingTasks(closeIntentCompletionSource.Task, ct));
    }
}
