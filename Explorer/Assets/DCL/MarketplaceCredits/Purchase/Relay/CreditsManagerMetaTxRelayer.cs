using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.WebRequests;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public enum RelayOutcome
    {
        BROADCAST,
        SIGNATURE_REJECTED,
        SIGNING_FAILED,
        RELAYER_REJECTED,
        AMBIGUOUS_BROADCAST,
    }

    public readonly struct RelayResult
    {
        public readonly RelayOutcome Outcome;
        public readonly string? TxHash;
        public readonly string? Message;

        public RelayResult(RelayOutcome outcome, string? txHash = null, string? message = null)
        {
            Outcome = outcome;
            TxHash = txHash;
            Message = message;
        }
    }

    // AI Generated on the base of the backend information
    public class CreditsManagerMetaTxRelayer
    {
        private const string ETH_SIGN_TYPED_DATA_METHOD = "eth_signTypedData_v4";
        private const string ETH_CALL_METHOD = "eth_call";

        private readonly IEthereumApi ethereumApi;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly CreditsChainConfig chainConfig;

        public CreditsManagerMetaTxRelayer(
            IEthereumApi ethereumApi,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            CreditsChainConfig chainConfig)
        {
            this.ethereumApi = ethereumApi;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.chainConfig = chainConfig;
        }

        public virtual async UniTask<RelayResult> RelayUseCreditsAsync(string buyer, string useCreditsCalldata, CancellationToken ct)
        {
            BigInteger nonce;

            try { nonce = await GetNonceAsync(buyer, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new RelayResult(RelayOutcome.SIGNING_FAILED, message: $"Nonce read failed: {e.Message}");
            }

            string typedDataJson = CreditsTradeEncoder.BuildMetaTxTypedDataJson(chainConfig, nonce, buyer, useCreditsCalldata);
            string signature;

            try
            {
                var signRequest = new EthApiRequest
                {
                    id = Guid.NewGuid().GetHashCode(),
                    method = ETH_SIGN_TYPED_DATA_METHOD,
                    @params = new object[] { buyer, typedDataJson },
                };

                EthApiResponse signResponse = await ethereumApi.SendAsync(signRequest, Web3RequestSource.Internal, ct);
                signature = signResponse.result?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(signature))
                    return new RelayResult(RelayOutcome.SIGNATURE_REJECTED, message: "Empty signature returned");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                bool userRejected = e.Message.IndexOf("reject", StringComparison.OrdinalIgnoreCase) >= 0
                                    || e.Message.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!userRejected)
                    ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));

                return new RelayResult(userRejected ? RelayOutcome.SIGNATURE_REJECTED : RelayOutcome.SIGNING_FAILED, message: e.Message);
            }

            string txData = CreditsTradeEncoder.BuildExecuteMetaTxCalldata(buyer, useCreditsCalldata, signature);

            var body = new JObject
            {
                ["transactionData"] = new JObject
                {
                    ["from"] = buyer,
                    ["params"] = new JArray { chainConfig.CreditsManagerAddress, txData },
                },
            };

            try
            {
                string relayerUrl = decentralandUrlsSource.Url(DecentralandUrl.MetaTransactionServer);

                JObject response = await webRequestController
                                        .PostAsync(new CommonArguments(URLAddress.FromString(relayerUrl)), GenericPostArguments.CreateJson(body.ToString(Newtonsoft.Json.Formatting.None)), ct, ReportCategory.CREDITS_PURCHASE)
                                        .CreateFromJson<JObject>(WRJsonParser.Newtonsoft);

                string? txHash = response["txHash"]?.ToString();

                if (string.IsNullOrEmpty(txHash))
                    return new RelayResult(RelayOutcome.RELAYER_REJECTED, message: response["message"]?.ToString() ?? "Relayer returned no txHash");

                return new RelayResult(RelayOutcome.BROADCAST, txHash);
            }
            catch (OperationCanceledException)
            {
                return new RelayResult(RelayOutcome.AMBIGUOUS_BROADCAST, message: "Cancelled while awaiting relayer response");
            }
            catch (UnityWebRequestException e)
            {
                if (e.ResponseCode > 0)
                    return new RelayResult(RelayOutcome.RELAYER_REJECTED, message: $"Relayer {e.ResponseCode}: {e.Text}");

                return new RelayResult(RelayOutcome.AMBIGUOUS_BROADCAST, message: e.Message);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new RelayResult(RelayOutcome.AMBIGUOUS_BROADCAST, message: e.Message);
            }
        }

        private async UniTask<BigInteger> GetNonceAsync(string buyer, CancellationToken ct)
        {
            var request = new EthApiRequest
            {
                readonlyNetwork = chainConfig.ReadonlyNetwork,
                id = Guid.NewGuid().GetHashCode(),
                method = ETH_CALL_METHOD,
                @params = new object[]
                {
                    new JObject
                    {
                        ["to"] = chainConfig.CreditsManagerAddress,
                        ["data"] = CreditsTradeEncoder.BuildGetNonceCalldata(buyer),
                    },
                    "latest",
                },
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, Web3RequestSource.Internal, ct);
            string hex = response.result?.ToString() ?? throw new InvalidOperationException("getNonce returned no result");
            string digits = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            if (digits.Length == 0)
                return BigInteger.Zero;

            return BigInteger.Parse($"0{digits}", NumberStyles.HexNumber);
        }
    }
}
