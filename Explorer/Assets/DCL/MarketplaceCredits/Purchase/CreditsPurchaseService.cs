using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using System;
using System.Security.Cryptography;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    // This service orchestrates the buy flow to mimic the shop one, it will resolve a trade, reserve the credits
    // launch the transaction and wait for the confirmation. It will release the credits if the transaction fails or is rejected, but will keep them if the transaction is broadcasted to avoid double spending.
    public class CreditsPurchaseService : ICreditsPurchaseService
    {
        private const int CENTS_PER_CREDIT = 10;
        private const long EXTERNAL_CALL_TTL_SECONDS = 60 * 60 * 24;
        private const string ETH_SEND_TRANSACTION = "eth_sendTransaction";
        private static readonly TimeSpan SETTLEMENT_TIMEOUT = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan RELEASE_INTENT_TIMEOUT = TimeSpan.FromSeconds(15);

        private readonly MarketplaceShopAPIClient shopAPIClient;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly CreditsManagerMetaTxRelayer metaTxRelayer;
        private readonly PolygonSettlementPoller settlementPoller;
        private readonly CreditsChainConfig chainConfig;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ICompositeWeb3Provider web3Provider;
        private readonly bool isFeatureEnabled;

        public event Action<CreditsPurchaseState>? StateChanged;

        public CreditsPurchaseService(
            MarketplaceShopAPIClient shopAPIClient,
            MarketplaceCreditsAPIClient creditsAPIClient,
            CreditsManagerMetaTxRelayer metaTxRelayer,
            PolygonSettlementPoller settlementPoller,
            CreditsChainConfig chainConfig,
            IWeb3IdentityCache identityCache,
            ICompositeWeb3Provider web3Provider,
            bool isFeatureEnabled)
        {
            this.shopAPIClient = shopAPIClient;
            this.creditsAPIClient = creditsAPIClient;
            this.metaTxRelayer = metaTxRelayer;
            this.settlementPoller = settlementPoller;
            this.chainConfig = chainConfig;
            this.identityCache = identityCache;
            this.web3Provider = web3Provider;
            this.isFeatureEnabled = isFeatureEnabled;
        }

        public async UniTask<CreditsPurchaseResult> PurchaseAsync(string tradeId, int expectedPriceCredits, CancellationToken ct)
        {
            if (!isFeatureEnabled)
                return new CreditsPurchaseResult(CreditsPurchaseError.FEATURE_DISABLED);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return new CreditsPurchaseResult(CreditsPurchaseError.UNKNOWN_ERROR, message: "No web3 identity");

            string buyer = identity.Address;

            try { return await PurchaseInternalAsync(tradeId, expectedPriceCredits, buyer, ct); }
            catch (OperationCanceledException)
            {
                return new CreditsPurchaseResult(CreditsPurchaseError.CANCELLED);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new CreditsPurchaseResult(CreditsPurchaseError.UNKNOWN_ERROR, message: e.Message);
            }
        }

        private async UniTask<CreditsPurchaseResult> PurchaseInternalAsync(string tradeId, int expectedPriceCredits, string buyer, CancellationToken ct)
        {
            SetState(CreditsPurchaseState.RESOLVING_LISTING);

            TradeDto? trade;

            try { trade = await shopAPIClient.GetTradeAsync(tradeId, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Trade {tradeId} could not be fetched: {e.Message}");
                return Fail(CreditsPurchaseError.LISTING_NOT_AVAILABLE, message: e.Message);
            }

            if (trade == null)
                return Fail(CreditsPurchaseError.LISTING_NOT_AVAILABLE);

            if (string.Equals(trade.signer, buyer, StringComparison.OrdinalIgnoreCase))
                return Fail(CreditsPurchaseError.OWN_LISTING);

            if (trade.received.Length == 0 || trade.received[0].assetType != CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA)
                return Fail(CreditsPurchaseError.LISTING_NOT_AVAILABLE, message: "Trade is not listed as credits");

            int usdCents = CreditsTradeEncoder.UsdWeiToCents(trade.received[0].amount);

            if (usdCents <= 0)
                return Fail(CreditsPurchaseError.LISTING_NOT_AVAILABLE, message: "Trade has no price");

            if (usdCents != expectedPriceCredits * CENTS_PER_CREDIT)
                return Fail(CreditsPurchaseError.PRICE_CHANGED, message: $"Listed for {usdCents} cents, expected {expectedPriceCredits * CENTS_PER_CREDIT}");

            SetState(CreditsPurchaseState.AUTHORIZING);

            AuthorizeCreditResponse authorization;

            try { authorization = await creditsAPIClient.AuthorizeUsdCreditAsync(usdCents, tradeId, ct); }
            catch (OperationCanceledException) { throw; }
            catch (UnityWebRequestException e)
            {
                bool insufficient = (e.Text ?? string.Empty).IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0
                                    || (e.Text ?? string.Empty).IndexOf("balance", StringComparison.OrdinalIgnoreCase) >= 0;

                return Fail(insufficient ? CreditsPurchaseError.INSUFFICIENT_CREDITS : CreditsPurchaseError.AUTHORIZATION_FAILED, message: e.Text);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return Fail(CreditsPurchaseError.AUTHORIZATION_FAILED, message: e.Message);
            }

            string useCreditsCalldata;

            try
            {
                useCreditsCalldata = CreditsTradeEncoder.BuildUseCreditsCalldata(
                    trade, buyer, authorization.credit, authorization.maxCreditedValue,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() + EXTERNAL_CALL_TTL_SECONDS,
                    RandomSalt());
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.ENCODING_FAILED, message: e.Message);
            }

            SetState(CreditsPurchaseState.SIGNING);

            RelayResult relay;

            try { relay = await metaTxRelayer.RelayUseCreditsAsync(buyer, useCreditsCalldata, ct); }
            catch (OperationCanceledException)
            {
                await ReleaseIntentAsync(authorization.credit.id);
                throw;
            }
            string? txHash = null;

            switch (relay.Outcome)
            {
                case RelayOutcome.BROADCAST:
                    txHash = relay.TxHash;
                    break;
                case RelayOutcome.SIGNATURE_REJECTED:
                    await ReleaseIntentAsync(authorization.credit.id);
                    return Fail(CreditsPurchaseError.SIGNATURE_REJECTED, message: relay.Message);
                case RelayOutcome.SIGNING_FAILED:
                    await ReleaseIntentAsync(authorization.credit.id);
                    return Fail(CreditsPurchaseError.SIGNING_FAILED, message: relay.Message);
                case RelayOutcome.AMBIGUOUS_BROADCAST:
                    SetState(CreditsPurchaseState.FAILED);
                    return new CreditsPurchaseResult(CreditsPurchaseError.SETTLEMENT_PENDING, message: relay.Message);
                case RelayOutcome.RELAYER_REJECTED:
                    if (web3Provider.IsThirdWebOTP)
                    {
                        await ReleaseIntentAsync(authorization.credit.id);
                        return Fail(CreditsPurchaseError.RELAYER_UNAVAILABLE, message: relay.Message);
                    }

                    SetState(CreditsPurchaseState.SUBMITTING);
                    CreditsPurchaseResult? fallbackFailure = null;

                    try { txHash = await SendWalletTransactionAsync(buyer, useCreditsCalldata, ct); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e)
                    {
                        bool userRejected = e.Message.IndexOf("reject", StringComparison.OrdinalIgnoreCase) >= 0
                                            || e.Message.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!userRejected)
                            ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));

                        fallbackFailure = Fail(userRejected ? CreditsPurchaseError.SIGNATURE_REJECTED : CreditsPurchaseError.RELAYER_UNAVAILABLE, message: e.Message);
                    }

                    if (fallbackFailure != null)
                    {
                        await ReleaseIntentAsync(authorization.credit.id);
                        return fallbackFailure.Value;
                    }

                    break;
            }

            if (string.IsNullOrEmpty(txHash))
            {
                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.RELAYER_UNAVAILABLE, message: "No transaction hash");
            }

            SetState(CreditsPurchaseState.WAITING_SETTLEMENT);

            SettlementOutcome settlement = await settlementPoller.WaitForSettlementAsync(txHash!, SETTLEMENT_TIMEOUT, ct);

            switch (settlement)
            {
                case SettlementOutcome.CONFIRMED:
                    SetState(CreditsPurchaseState.SUCCESS);
                    return CreditsPurchaseResult.Ok(txHash!);
                case SettlementOutcome.REVERTED:
                    await ReleaseIntentAsync(authorization.credit.id);
                    return Fail(CreditsPurchaseError.TRANSACTION_REVERTED, txHash);
                default:
                    SetState(CreditsPurchaseState.FAILED);
                    return new CreditsPurchaseResult(CreditsPurchaseError.SETTLEMENT_PENDING, txHash);
            }
        }

        private async UniTask<string?> SendWalletTransactionAsync(string buyer, string useCreditsCalldata, CancellationToken ct)
        {
            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = ETH_SEND_TRANSACTION,
                @params = new object[]
                {
                    new JObject
                    {
                        ["from"] = buyer,
                        ["to"] = chainConfig.CreditsManagerAddress,
                        ["data"] = useCreditsCalldata,
                    },
                },
            };

            EthApiResponse response = await web3Provider.SendAsync(request, Web3RequestSource.Internal, ct);
            return response.result?.ToString();
        }

        private async UniTask ReleaseIntentAsync(string creditId)
        {
            using var timeoutCts = new CancellationTokenSource(RELEASE_INTENT_TIMEOUT);

            try { await creditsAPIClient.ReleaseUsdIntentsAsync(new[] { creditId }, timeoutCts.Token); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Failed to release credit intent {creditId}: {e.Message}");
            }
        }

        private CreditsPurchaseResult Fail(CreditsPurchaseError error, string? txHash = null, string? message = null)
        {
            SetState(CreditsPurchaseState.FAILED);
            return new CreditsPurchaseResult(error, txHash, message);
        }

        private void SetState(CreditsPurchaseState state) =>
            StateChanged?.Invoke(state);

        private static byte[] RandomSalt()
        {
            var salt = new byte[32];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            return salt;
        }
    }
}
