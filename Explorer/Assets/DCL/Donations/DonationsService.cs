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
        private const string MAIN_NET_CONTRACT_ADDRESS = "0x0F5D2fB29fb7d3CFeE444a200298f468908cC942";
        private const string SEPOLIA_NET_CONTRACT_ADDRESS = "0xB3C6F2cB6dBf3BfC4c6E3E5D1E8b8D7F4F5A6B7C8";
        private const string MANA_BALANCE_FUNCTION_SELECTOR = "0x70a08231";

        private static readonly URLAddress MANA_USD_API_URL = URLAddress.FromString("https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd");

        public IReadonlyReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> DonationsEnabledCurrentScene => donationsEnabledCurrentScene;
        private readonly ReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> donationsEnabledCurrentScene = new ((false, null, null));

        private readonly IScenesCache scenesCache;
        private readonly IEthereumApi ethereumApi;
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realmData;
        private readonly IPlacesAPIService placesAPIService;
        private readonly string contractAddress;

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

            contractAddress = dclEnvironment == DecentralandEnvironment.Org ? MAIN_NET_CONTRACT_ADDRESS : SEPOLIA_NET_CONTRACT_ADDRESS;
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

        public async UniTask<float> GetCurrentBalanceAsync(CancellationToken ct)
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

            return (float)weiValue / 1_000_000_000_000_000_000f;
        }

        public async UniTask<float> GetCurrentManaConversionAsync(CancellationToken ct)
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
