using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.Web3;
using ECS.SceneLifeCycle;
using MVC;
using System;
using System.Threading;
using Utility;
using Newtonsoft.Json.Linq;

namespace DCL.Donations.UI
{
    public class DonationsPanelController : ControllerBase<DonationsPanelView>
    {
        private readonly IEthereumApi ethereumApi;
        private readonly IScenesCache scenesCache;
        private readonly IProfileRepository profileRepository;
        private readonly float recommendedDonationAmount;

        private CancellationTokenSource panelLifecycleCts = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public DonationsPanelController(ViewFactoryMethod viewFactory,
            IEthereumApi ethereumApi,
            IScenesCache scenesCache,
            IProfileRepository profileRepository,
            float recommendedDonationAmount)
            : base(viewFactory)
        {
            this.ethereumApi = ethereumApi;
            this.scenesCache = scenesCache;
            this.profileRepository = profileRepository;
            this.recommendedDonationAmount = recommendedDonationAmount;
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
            panelLifecycleCts.Token.ThrowIfCancellationRequested();
            closeIntentCompletionSource = new UniTaskCompletionSource();
            LoadDataAsync(panelLifecycleCts.Token).Forget();
        }

        private async UniTaskVoid LoadDataAsync(CancellationToken ct)
        {
            try
            {
                viewInstance!.SetLoadingState(true);
                string? creatorAddress = scenesCache.CurrentScene.Value?.SceneData.SceneEntityDefinition.metadata.creator;

                if (creatorAddress == null)
                {
                    CloseController();
                    return;
                }

                Profile? creatorProfile = await profileRepository.GetAsync(creatorAddress, ct);
                EthApiResponse currentBalanceResponse = await GetCurrentBalanceAsync(ct);

                viewInstance!.ConfigurePanel(creatorProfile, 0, recommendedDonationAmount,0); // TODO: Fill with real values
            }
            catch (OperationCanceledException)
            {
                CloseController();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.DONATIONS);
                CloseController();
            }
            finally { viewInstance!.SetLoadingState(false); }
        }

        private async UniTask<EthApiResponse> GetCurrentBalanceAsync(CancellationToken ct)
        {
            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_getBalance",
                @params = new object[]
                {
                    new JObject
                    {
                        ["address"] = ViewDependencies.CurrentIdentity?.Address.ToString()
                    }
                }
            };
            return await ethereumApi.SendAsync(request, ct);
        }

        protected override void OnViewClose()
        {
            panelLifecycleCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetClosingTasks(closeIntentCompletionSource.Task, ct));
    }
}
