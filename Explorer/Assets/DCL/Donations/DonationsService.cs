using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Utilities;
using DCL.Web3;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
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
    public class DonationsService : IDisposable
    {
        private const string POLYGON_CONTRACT_ADDRESS = "0xA1c57f48F0Deb89f569dFbE6E2B7f46D33606fD4";
        private const string SEPOLIA_NET_CONTRACT_ADDRESS = "0xFa04D2e2BA9aeC166c93dFEEba7427B2303beFa9";

        private const string MANA_BALANCE_FUNCTION_SELECTOR = "0x70a08231";
        private const string TRANSFER_FUNCTION_SELECTOR = "0xa9059cbb";
        private const decimal WEI_FACTOR = 1_000_000_000_000_000_000;

        private const double MANA_RATE_CACHE_DURATION_MINUTES = 30;

        private static readonly URLAddress MANA_USD_API_URL = URLAddress.FromString("https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd");

        public IReadonlyReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> DonationsEnabledCurrentScene => donationsEnabledCurrentScene;
        private readonly ReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> donationsEnabledCurrentScene = new ((false, null, null));

        private readonly IScenesCache scenesCache;
        private readonly IEthereumApi ethereumApi;
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realmData;
        private readonly IPlacesAPIService placesAPIService;
        private readonly string contractAddress;

        private DateTime lastManaRateQueryTime = new ();
        private decimal lastManaRate;

        public DonationsService(IScenesCache scenesCache,
            IEthereumApi ethereumApi,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IPlacesAPIService placesAPIService,
            DecentralandEnvironment dclEnvironment)
        {
            this.scenesCache = scenesCache;
            this.ethereumApi = ethereumApi;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.placesAPIService = placesAPIService;

            contractAddress = dclEnvironment == DecentralandEnvironment.Org ? POLYGON_CONTRACT_ADDRESS : SEPOLIA_NET_CONTRACT_ADDRESS;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;
        }

        public void Dispose()
        {
            scenesCache.CurrentScene.OnUpdate -= OnCurrentSceneChanged;
        }

        private void OnCurrentSceneChanged(ISceneFacade? currentScene)
        {
            string? creatorAddress = currentScene?.SceneData.GetCreatorAddress();
            donationsEnabledCurrentScene.UpdateValue((creatorAddress != null, creatorAddress, currentScene?.Info.BaseParcel));
        }

        public async UniTask<string> GetSceneNameAsync(Vector2Int parcelPosition, CancellationToken ct)
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

        public async UniTask<decimal> GetCurrentBalanceAsync(CancellationToken ct)
        {
            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new JObject
                    {
                        ["to"] = contractAddress,
                        ["data"] = $"{MANA_BALANCE_FUNCTION_SELECTOR}000000000000000000000000{ViewDependencies.CurrentIdentity?.Address.ToString()[2..]}"
                    },
                    "latest"
                }
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, ct);

            BigInteger weiValue = BigInteger.Parse(response.result.ToString()[2..], NumberStyles.HexNumber);

            return (decimal)weiValue / WEI_FACTOR;
        }

        public async UniTask<string> SendDonationAsync(string toAddress, decimal amountInMana, CancellationToken ct)
        {
            BigInteger value = new BigInteger(decimal.Round(amountInMana * WEI_FACTOR, 0, MidpointRounding.AwayFromZero));
            string to = toAddress[2..];

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
                        ["data"] = $"{TRANSFER_FUNCTION_SELECTOR}000000000000000000000000{to}{value.ToString("x")}"
                    }
                }
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, ct);

            return response.result.ToString();
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
