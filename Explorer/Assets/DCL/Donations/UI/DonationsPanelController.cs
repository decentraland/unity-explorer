using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DCL.WebRequests;
using ECS;
using MVC;
using System;
using System.Threading;
using Utility;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;
using Utility.Arch;

namespace DCL.Donations.UI
{
    public class DonationsPanelController : ControllerBase<DonationsPanelView>
    {
        private static readonly URN EMOTE_MONEY_URN = new ("money");
        private static readonly URLAddress MANA_USD_API_URL = URLAddress.FromString("https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd");

        private readonly IEthereumApi ethereumApi;
        private readonly DonationsService donationsService;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly float recommendedDonationAmount;
        private readonly Entity playerEntity;
        private readonly World world;
        private readonly IRealmData realmData;
        private readonly IPlacesAPIService placesAPIService;

        private CancellationTokenSource panelLifecycleCts = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public DonationsPanelController(ViewFactoryMethod viewFactory,
            IEthereumApi ethereumApi,
            DonationsService donationsService,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            World world,
            Entity playerEntity,
            IRealmData realmData,
            IPlacesAPIService placesAPIService,
            float recommendedDonationAmount)
            : base(viewFactory)
        {
            this.ethereumApi = ethereumApi;
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.world = world;
            this.playerEntity = playerEntity;
            this.recommendedDonationAmount = recommendedDonationAmount;
            this.realmData = realmData;
            this.placesAPIService = placesAPIService;
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
            CloseController();
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
                var donationStatus = donationsService.DonationsEnabledCurrentScene.Value;

                if (!donationStatus.enabled)
                {
                    CloseController();
                    return;
                }

                Profile? creatorProfile = await profileRepository.GetAsync(donationStatus.creatorAddress!, ct, IProfileRepository.BatchBehaviour.ENFORCE_SINGLE_GET, CatalystRetryPolicy.SIMPLE);
                // Scene creators can set a wallet that has nothing to do with DCL, so we can safely log this information to ignore 404s
                if (creatorProfile == null)
                    ReportHub.LogException(new Exception($"Previous 404 on profile {donationStatus.creatorAddress} can be ignored as the wallet might not be stored in catalysts"), ReportCategory.DONATIONS);
                //EthApiResponse currentBalanceResponse = await GetCurrentBalanceAsync(ct);
                float manaPriceUsd = await GetCurrentManaConversionAsync(ct);
                string sceneName = await GetSceneNameAsync(donationStatus.baseParcel!.Value, ct);

                viewInstance!.ConfigurePanel(creatorProfile, donationStatus.creatorAddress!,
                    sceneName, 0,
                    recommendedDonationAmount, manaPriceUsd,
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

        private async UniTask<string> GetSceneNameAsync(Vector2Int parcelPosition, CancellationToken ct)
        {
            PlacesData.PlaceInfo? placeInfo = await GetPlaceInfoAsync(parcelPosition, ct);

            if (realmData.ScenesAreFixed)
                return realmData.RealmName.Replace(".dcl.eth", string.Empty);

            return placeInfo?.title ?? "Unknown place";
        }

        private async UniTask<PlacesData.PlaceInfo?> GetPlaceInfoAsync(Vector2Int parcelPosition, CancellationToken ct)
        {
            await realmData.WaitConfiguredAsync();

            try
            {
                if (realmData.ScenesAreFixed)
                    return null;

                return await placesAPIService.GetPlaceAsync(parcelPosition, ct);
            }
            catch (NotAPlaceException notAPlaceException)
            {
                ReportHub.LogWarning(ReportCategory.DONATIONS, $"Not a place requested: {notAPlaceException.Message}");
                return null;
            }
            catch (Exception)
            {
                return null;
            }
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

        private async UniTask<float> GetCurrentManaConversionAsync(CancellationToken ct)
        {
            var response = await webRequestController.GetAsync(
                                           new CommonArguments(MANA_USD_API_URL),
                                           ct,
                                           ReportCategory.DONATIONS)
                                      .CreateFromJson<Dictionary<string, Dictionary<string, float>>>(WRJsonParser.Newtonsoft);
            return response["decentraland"]["usd"];
        }
    }
}
