using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
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
        // https://contracts.decentraland.org/addresses.json
        // Prod Matic MANA contract
        private const string MATIC_CONTRACT_ADDRESS = "0xA1c57f48F0Deb89f569dFbE6E2B7f46D33606fD4";
        // Test Amoy MANA contract
        private const string AMOY_NET_CONTRACT_ADDRESS = "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0";

        private const string MANA_BALANCE_FUNCTION_SELECTOR = "0x70a08231";
        private const string TRANSFER_FUNCTION_SELECTOR = "0xa9059cbb";
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
        private readonly bool donationFeatureEnabled;

        private DateTime lastManaRateQueryTime;
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

            contractAddress = dclEnvironment == DecentralandEnvironment.Org ? MATIC_CONTRACT_ADDRESS : AMOY_NET_CONTRACT_ADDRESS;
            donationFeatureEnabled = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.DONATIONS) || Application.isEditor || true; //TODO: remove '|| true' when feature is ready to be shipped
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

        private static string LeftPad64(string hex)
        {
            // Ensure no 0x prefix before padding
            string s = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            if (s.Length > 64)
                throw new ArgumentException($"Argument too large: {s.Length} chars. Max 64 hex chars allowed.");

            // Pad with zeros on the left to reach 64 characters
            return s.PadLeft(64, '0').ToLowerInvariant();
        }

        private static string NormalizeAddress(string addr)
        {
            // Remove '0x' prefix if present
            string s = addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? addr[2..] : addr;

            if (s.Length != 40)
                throw new ArgumentException($"Invalid address length: {s.Length}. Expected 40 hex characters.");

            return s.ToLowerInvariant();
        }

        public async UniTask<decimal> GetCurrentBalanceAsync(CancellationToken ct)
        {
            string address = LeftPad64(NormalizeAddress(ViewDependencies.CurrentIdentity?.Address));

            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new JObject
                    {
                        ["to"] = contractAddress,
                        ["data"] = $"{MANA_BALANCE_FUNCTION_SELECTOR}{address}"
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
            BigInteger value = new BigInteger(decimal.Round(amountInMana * WEI_FACTOR, 0, MidpointRounding.AwayFromZero));
            string to = LeftPad64(NormalizeAddress(toAddress));
            string weiAmountString = LeftPad64(value.ToString("x"));

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
                        ["data"] = $"{TRANSFER_FUNCTION_SELECTOR}{to}{weiAmountString}"
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
