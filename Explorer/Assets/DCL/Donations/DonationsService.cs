using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Utils;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Utilities;
using DCL.Web3;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using Global.AppArgs;
using MVC;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using UnityEngine;

namespace DCL.Donations
{
    public class DonationsService : IDonationsService
    {
        private const string UNKNOWN_PLACE_TEXT = "Unknown place";

        // https://contracts.decentraland.org/addresses.json
        // Prod Matic MANA contract
        private const string MATIC_CONTRACT_ADDRESS = "0xA1c57f48F0Deb89f569dFbE6E2B7f46D33606fD4";
        // Test Amoy MANA contract
        private const string AMOY_NET_CONTRACT_ADDRESS = "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0";

        private const string MATIC_NETWORK = "polygon";
        private const string AMOY_NETWORK = "amoy";

        private const decimal WEI_FACTOR = 1_000_000_000_000_000_000;
        private const double MANA_RATE_CACHE_DURATION_MINUTES = 30;

        private static readonly URLAddress MANA_USD_API_URL = URLAddress.FromString("https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd");

        public IReadonlyReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> DonationsEnabledCurrentScene => donationsEnabledCurrentScene;
        private readonly ReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> donationsEnabledCurrentScene = new ((false, null, null));

        public bool DonationFeatureEnabled => donationFeatureEnabled;

        private readonly IScenesCache scenesCache;
        private readonly IEthereumApi ethereumApi;
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realmData;
        private readonly IPlacesAPIService placesAPIService;
        private readonly string contractAddress;
        private readonly string networkName;
        private readonly bool donationFeatureEnabled;

        private DateTime lastManaRateQueryTime;
        private decimal lastManaRate;

        public DonationsService(IScenesCache scenesCache,
            IEthereumApi ethereumApi,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IPlacesAPIService placesAPIService,
            DecentralandEnvironment dclEnvironment,
            IAppArgs appArgs)
        {
            this.scenesCache = scenesCache;
            this.ethereumApi = ethereumApi;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.placesAPIService = placesAPIService;

            switch (dclEnvironment)
            {
                case DecentralandEnvironment.Org:
                case DecentralandEnvironment.Today:
                        contractAddress = MATIC_CONTRACT_ADDRESS;
                        networkName = MATIC_NETWORK;
                        break;
                default:
                        contractAddress = AMOY_NET_CONTRACT_ADDRESS;
                        networkName = AMOY_NETWORK;
                        break;
            }

            donationFeatureEnabled = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.DONATIONS) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.DONATIONS_UI));
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;
        }

        public void Dispose()
        {
            scenesCache.CurrentScene.OnUpdate -= OnCurrentSceneChanged;
        }

        private void OnCurrentSceneChanged(ISceneFacade? currentScene)
        {
            string? creatorAddress = currentScene?.SceneData.GetCreatorAddress();
            donationsEnabledCurrentScene.UpdateValue((creatorAddress != null && donationFeatureEnabled, creatorAddress, currentScene?.Info.BaseParcel));
        }

        public async UniTask<string> GetSceneNameAsync(Vector2Int parcelPosition, CancellationToken ct)
        {
            PlacesData.PlaceInfo? placeInfo = await GetPlaceInfoAsync(parcelPosition, ct);

            if (realmData.ScenesAreFixed)
                return realmData.RealmName.Replace(".dcl.eth", string.Empty);

            return placeInfo?.title ?? UNKNOWN_PLACE_TEXT;
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

        public async UniTask<decimal> GetCurrentBalanceAsync(CancellationToken ct)
        {
            string data = ManualTxEncoder.EncodeGetBalance(ViewDependencies.CurrentIdentity?.Address);

            var request = new EthApiRequest
            {
                readonlyNetwork = networkName,
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new JObject
                    {
                        ["to"] = contractAddress,
                        ["data"] = data
                    },
                    "latest"
                }
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, ct);

            string weiString = response.result.ToString()[2..];

            BigInteger weiValue = BigInteger.Parse(string.IsNullOrEmpty(weiString) ? "0" : weiString, NumberStyles.HexNumber);

            return (decimal)weiValue / WEI_FACTOR;
        }

        public async UniTask<bool> SendDonationAsync(string toAddress, decimal amountInMana, CancellationToken ct)
        {
            string data = ManualTxEncoder.EncodeSendDonation(toAddress, amountInMana);

            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_sendTransaction",
                @params = new object[]
                {
                    new JObject
                    {
                        ["from"] = ViewDependencies.CurrentIdentity?.Address.ToString(),
                        ["to"] = contractAddress,
                        ["value"] = "0x0",
                        ["data"] = data
                    }
                }
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, ct);

            if (response.result != null)
                ReportHub.Log(ReportCategory.DONATIONS, $"Donation was successful. Tx hash: {response.result}");

            return response.result != null;
        }

        public async UniTask<decimal> GetCurrentManaConversionAsync(CancellationToken ct)
        {
            if (lastManaRateQueryTime.AddMinutes(MANA_RATE_CACHE_DURATION_MINUTES) > DateTime.UtcNow)
                return lastManaRate;

            var response = await webRequestController.GetAsync(
                                                          new CommonArguments(MANA_USD_API_URL),
                                                          ct,
                                                          ReportCategory.DONATIONS)
                                                     .CreateFromJson<Dictionary<string, Dictionary<string, decimal>>>(WRJsonParser.Newtonsoft);

            lastManaRate = response["decentraland"]["usd"];
            lastManaRateQueryTime = DateTime.UtcNow;

            return lastManaRate;
        }
    }
}
