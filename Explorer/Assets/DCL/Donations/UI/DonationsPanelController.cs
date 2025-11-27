using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DCL.WebRequests;
using ECS.SceneLifeCycle;
using MVC;
using System;
using System.Threading;
using Utility;
using Newtonsoft.Json.Linq;
using Utility.Arch;

namespace DCL.Donations.UI
{
    public class DonationsPanelController : ControllerBase<DonationsPanelView>
    {
        private static readonly URN EMOTE_MONEY_URN = new ("money");
        private static readonly URLAddress MANA_USD_API_URL = URLAddress.FromString("https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd");

        private readonly IEthereumApi ethereumApi;
        private readonly IScenesCache scenesCache;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly float recommendedDonationAmount;
        private readonly Entity playerEntity;
        private readonly World world;

        private CancellationTokenSource panelLifecycleCts = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public DonationsPanelController(ViewFactoryMethod viewFactory,
            IEthereumApi ethereumApi,
            IScenesCache scenesCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            World world,
            Entity playerEntity,
            float recommendedDonationAmount)
            : base(viewFactory)
        {
            this.ethereumApi = ethereumApi;
            this.scenesCache = scenesCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.world = world;
            this.playerEntity = playerEntity;
            this.recommendedDonationAmount = recommendedDonationAmount;
        }

        public override void Dispose()
        {
            panelLifecycleCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.SendDonationRequested += OnSendDonationRequested;
        }

        private void CloseController() =>
            closeIntentCompletionSource.TrySetResult();

        protected override void OnViewInstantiated()
        {
            viewInstance!.SendDonationRequested += OnSendDonationRequested;
        }

        protected override void OnBeforeViewShow()
        {
            panelLifecycleCts = panelLifecycleCts.SafeRestart();
            panelLifecycleCts.Token.ThrowIfCancellationRequested();
            closeIntentCompletionSource = new UniTaskCompletionSource();
            LoadDataAsync(panelLifecycleCts.Token).Forget();
        }

        private void OnSendDonationRequested(string creatorAddress, float amount)
        {
            //TODO: Implement donation sending flow
            PlayEmoteByUrn(EMOTE_MONEY_URN);
        }

        private void PlayEmoteByUrn(URN emoteUrn)
        {
            world.AddOrSet(playerEntity, new CharacterEmoteIntent
            {
                EmoteId = emoteUrn,
                Spatial = true,
                TriggerSource = TriggerSource.SELF
            });
        }

        private async UniTaskVoid LoadDataAsync(CancellationToken ct)
        {
            try
            {
                viewInstance!.SetLoadingState(true);
                string? creatorAddress = scenesCache.CurrentScene.Value?.SceneData.GetCreatorAddress();

                if (creatorAddress == null || scenesCache.CurrentScene.Value == null)
                {
                    CloseController();
                    return;
                }

                Profile? creatorProfile = await profileRepository.GetAsync(creatorAddress, ct);
                EthApiResponse currentBalanceResponse = await GetCurrentBalanceAsync(ct);
                ManaPriceResponse manaPriceResponse = await GetCurrentManaConversionAsync(ct);

                viewInstance!.ConfigurePanel(creatorProfile, scenesCache.CurrentScene.Value, 0,
                    recommendedDonationAmount, manaPriceResponse.decentraland.usd,
                    profileRepositoryWrapper); // TODO: Fill with real values
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

        private async UniTask<ManaPriceResponse> GetCurrentManaConversionAsync(CancellationToken ct) =>
            await webRequestController.GetAsync(
                                           new CommonArguments(MANA_USD_API_URL),
                                           ct,
                                           ReportCategory.DONATIONS)
                                      .CreateFromJson<ManaPriceResponse>(WRJsonParser.Newtonsoft);

        [Serializable]
        private readonly struct ManaPriceResponse
        {
            public readonly Coin decentraland;

            [Serializable]
            public readonly struct Coin
            {
                public readonly float usd;
            }
        }
    }
}
