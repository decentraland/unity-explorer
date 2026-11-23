using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.WebRequests;
using Newtonsoft.Json.Linq;
using System;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public enum RelayOutcome
    {
        Broadcast,
        SignatureRejected,
        SigningFailed,
        SignatureTimedOut,
        RelayerRejected,
        AmbiguousBroadcast,
    }

    public readonly struct RelayResult
    {
        public readonly RelayOutcome Outcome;
        public readonly string? TxHash;
        public readonly string? Message;

        /// <summary>The meta-transaction nonce that was signed; negative when signing never happened.</summary>
        public readonly BigInteger Nonce;

        public RelayResult(RelayOutcome outcome, string? txHash = null, string? message = null, BigInteger? nonce = null)
        {
            Outcome = outcome;
            TxHash = txHash;
            Message = message;
            Nonce = nonce ?? BigInteger.MinusOne;
        }
    }

    /// <summary>
    ///     Relays a call as a gasless meta-transaction: reads the buyer's nonce on the contract, asks the wallet to
    ///     sign the typed data and posts the signed transaction to the transactions server.
    /// </summary>
    public class MetaTxRelayer
    {
        /// <summary>
        ///     The transactions server refuses a sale relayed straight to the marketplace or the CollectionStore
        ///     when any price in the call is below this (its MIN_SALE_VALUE_IN_WEI, checked per entry of a batch).
        ///     It compares the amount as the call carries it: MANA wei for a mint and a MANA-priced trade, USD wei
        ///     for a USD-pegged trade. Calls through the CreditsManager are not priced.
        /// </summary>
        public static readonly BigInteger MIN_DIRECT_SALE_VALUE_WEI = BigInteger.Pow(10, 18);

        private const string ETH_SIGN_TYPED_DATA_METHOD = "eth_signTypedData_v4";
        private const int HTTP_INTERNAL_SERVER_ERROR = 500;
        private static readonly TimeSpan DEFAULT_SIGNATURE_TIMEOUT = TimeSpan.FromMinutes(5);

        private readonly TimeSpan signatureTimeout;
        private readonly IEthereumApi ethereumApi;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly CreditsChainConfig chainConfig;

        public MetaTxRelayer(
            IEthereumApi ethereumApi,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            CreditsChainConfig chainConfig) : this(ethereumApi, webRequestController, decentralandUrlsSource, chainConfig, DEFAULT_SIGNATURE_TIMEOUT) { }

        internal MetaTxRelayer(
            IEthereumApi ethereumApi,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            CreditsChainConfig chainConfig,
            TimeSpan signatureTimeout)
        {
            this.signatureTimeout = signatureTimeout;
            this.ethereumApi = ethereumApi;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.chainConfig = chainConfig;
        }

        /// <summary>
        ///     Signs and relays one call as a meta-transaction of the given contract. minNonce floors the nonce read
        ///     from the contract (negative for no floor): a call signed right after another one's receipt may still
        ///     read the previous nonce from a lagging RPC node, and reusing it would make the relayer reject it.
        ///     <paramref name="signCt" /> covers the nonce read and the wallet signature and is rethrown when cancelled;
        ///     a signature that is neither given nor refused within the timeout ends as <see cref="RelayOutcome.SignatureTimedOut" />;
        ///     <paramref name="broadcastCt" /> covers the relayer request, where a cancellation is an ambiguous broadcast.
        /// </summary>
        public virtual async UniTask<RelayResult> RelayAsync(MetaTxContract contract, string buyer, string calldata, BigInteger minNonce, CancellationToken signCt, CancellationToken broadcastCt)
        {
            BigInteger nonce;

            try { nonce = await GetNonceAsync(contract.Address, buyer, signCt); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new RelayResult(RelayOutcome.SigningFailed, message: $"Nonce read failed: {e.Message}");
            }

            if (minNonce > nonce)
                nonce = minNonce;

            string typedDataJson = CreditsTradeEncoder.BuildMetaTxTypedDataJson(contract, chainConfig.ChainId, nonce, buyer, calldata);
            (string signature, RelayResult? signFailure) = await SignAsync(buyer, typedDataJson, signCt);

            if (signFailure != null)
                return signFailure.Value;

            string txData;

            try { txData = CreditsTradeEncoder.BuildExecuteMetaTxCalldata(contract, buyer, calldata, signature); }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new RelayResult(RelayOutcome.SigningFailed, message: $"Unusable signature: {e.Message}");
            }

            var body = new JObject
            {
                ["transactionData"] = new JObject
                {
                    ["from"] = buyer,
                    ["params"] = new JArray { contract.Address, txData },
                },
            };

            try
            {
                string relayerUrl = decentralandUrlsSource.Url(DecentralandUrl.MetaTransactionServer);

                JObject response = await webRequestController
                                        .PostAsync(new CommonArguments(URLAddress.FromString(relayerUrl)), GenericPostArguments.CreateJson(body.ToString(Newtonsoft.Json.Formatting.None)), broadcastCt, ReportCategory.CREDITS_PURCHASE)
                                        .CreateFromJson<JObject>(WRJsonParser.Newtonsoft);

                string? txHash = response["txHash"]?.ToString();

                if (string.IsNullOrEmpty(txHash))
                    return new RelayResult(RelayOutcome.RelayerRejected, message: response["message"]?.ToString() ?? "Relayer returned no txHash");

                return new RelayResult(RelayOutcome.Broadcast, txHash, nonce: nonce);
            }
            catch (OperationCanceledException)
            {
                return new RelayResult(RelayOutcome.AmbiguousBroadcast, message: "Cancelled while awaiting relayer response", nonce: nonce);
            }
            catch (UnityWebRequestException e)
            {
                if (IsProvenRejection(e.ResponseCode))
                    return new RelayResult(RelayOutcome.RelayerRejected, message: $"Relayer {e.ResponseCode}: {e.Text}");

                return new RelayResult(RelayOutcome.AmbiguousBroadcast, message: e.Message, nonce: nonce);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new RelayResult(RelayOutcome.AmbiguousBroadcast, message: e.Message, nonce: nonce);
            }
        }

        /// <summary>
        ///     Only a 4xx proves the relayer refused the transaction. A 5xx can come from a gateway, or from the relayer
        ///     after it already submitted, and no response code at all means the request may still have been received.
        /// </summary>
        internal static bool IsProvenRejection(long responseCode) =>
            responseCode is > 0 and < HTTP_INTERNAL_SERVER_ERROR;

        /// <summary>
        ///     Asks the wallet for the signature. Returns the failure when it was refused, failed or was not given
        ///     within the timeout; rethrows a cancellation of <paramref name="signCt" />.
        /// </summary>
        private async UniTask<(string signature, RelayResult? failure)> SignAsync(string buyer, string typedDataJson, CancellationToken signCt)
        {
            using CancellationTokenSource signatureCts = CancellationTokenSource.CreateLinkedTokenSource(signCt);
            using IDisposable timeoutTimer = signatureCts.CancelAfterSlim(signatureTimeout);

            try
            {
                var signRequest = new EthApiRequest
                {
                    id = Guid.NewGuid().GetHashCode(),
                    method = ETH_SIGN_TYPED_DATA_METHOD,
                    @params = new object[] { buyer, typedDataJson },
                };

                EthApiResponse signResponse = await ethereumApi.SendAsync(signRequest, Web3RequestSource.Internal, signatureCts.Token);
                string signature = signResponse.result?.ToString() ?? string.Empty;

                return string.IsNullOrEmpty(signature)
                    ? (string.Empty, new RelayResult(RelayOutcome.SignatureRejected, message: "Empty signature returned"))
                    : (signature, null);
            }
            catch (OperationCanceledException) when (signatureCts.IsCancellationRequested && !signCt.IsCancellationRequested)
            {
                return (string.Empty, new RelayResult(RelayOutcome.SignatureTimedOut, message: $"No signature within {signatureTimeout.TotalSeconds:F0}s"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                bool userRejected = e.Message.IndexOf("reject", StringComparison.OrdinalIgnoreCase) >= 0
                                    || e.Message.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!userRejected)
                    ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));

                return (string.Empty, new RelayResult(userRejected ? RelayOutcome.SignatureRejected : RelayOutcome.SigningFailed, message: e.Message));
            }
        }

        private UniTask<BigInteger> GetNonceAsync(string contractAddress, string buyer, CancellationToken ct) =>
            EthCallReader.ReadUint256Async(ethereumApi, chainConfig.ReadonlyNetwork, contractAddress, CreditsTradeEncoder.BuildGetNonceCalldata(buyer), ct);
    }
}
