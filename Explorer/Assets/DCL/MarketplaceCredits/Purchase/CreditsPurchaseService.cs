using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    // This service orchestrates the buy flow to mimic the shop one: it quotes the trade at the rate settlement
    // uses, reserves the credits, relays one gasless transaction and waits for the confirmation. It will release
    // the credits if the transaction fails or is rejected, but will keep them if the transaction is broadcasted
    // to avoid double spending.
    public class CreditsPurchaseService : ICreditsPurchaseService
    {
        private const int CENTS_PER_CREDIT = 10;
        private const long EXTERNAL_CALL_TTL_SECONDS = 60 * 60 * 24;
        private static readonly TimeSpan SETTLEMENT_TIMEOUT = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan RELEASE_INTENT_TIMEOUT = TimeSpan.FromSeconds(15);

        private readonly MarketplaceShopAPIClient shopAPIClient;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly CreditsManagerMetaTxRelayer metaTxRelayer;
        private readonly PolygonSettlementPoller settlementPoller;
        private readonly ManaUsdRateReader manaUsdRateReader;
        private readonly IWeb3IdentityCache identityCache;
        private readonly bool isFeatureEnabled;

        public event Action<CreditsPurchaseState>? StateChanged;

        public CreditsPurchaseService(
            MarketplaceShopAPIClient shopAPIClient,
            MarketplaceCreditsAPIClient creditsAPIClient,
            CreditsManagerMetaTxRelayer metaTxRelayer,
            PolygonSettlementPoller settlementPoller,
            ManaUsdRateReader manaUsdRateReader,
            IWeb3IdentityCache identityCache,
            bool isFeatureEnabled)
        {
            this.shopAPIClient = shopAPIClient;
            this.creditsAPIClient = creditsAPIClient;
            this.metaTxRelayer = metaTxRelayer;
            this.settlementPoller = settlementPoller;
            this.manaUsdRateReader = manaUsdRateReader;
            this.identityCache = identityCache;
            this.isFeatureEnabled = isFeatureEnabled;
        }

        public async UniTask<CreditsQuoteResult> QuoteAsync(string tradeId, CancellationToken ct)
        {
            if (!isFeatureEnabled)
                return new CreditsQuoteResult(CreditsPurchaseError.FeatureDisabled);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return new CreditsQuoteResult(CreditsPurchaseError.UnknownError, "No web3 identity");

            SetState(CreditsPurchaseState.ResolvingListing);

            try { return await QuoteInternalAsync(tradeId, identity.Address, ct); }
            catch (OperationCanceledException)
            {
                return new CreditsQuoteResult(CreditsPurchaseError.Cancelled);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new CreditsQuoteResult(CreditsPurchaseError.UnknownError, e.Message);
            }
        }

        public async UniTask<CreditsPurchaseResult> PurchaseAsync(CreditsPurchaseQuote quote, CancellationToken ct)
        {
            if (!isFeatureEnabled)
                return new CreditsPurchaseResult(CreditsPurchaseError.FeatureDisabled);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return new CreditsPurchaseResult(CreditsPurchaseError.UnknownError, message: "No web3 identity");

            try { return await PurchaseInternalAsync(quote, identity.Address, ct); }
            catch (OperationCanceledException)
            {
                return new CreditsPurchaseResult(CreditsPurchaseError.Cancelled);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return new CreditsPurchaseResult(CreditsPurchaseError.UnknownError, message: e.Message);
            }
        }

        private async UniTask<CreditsQuoteResult> QuoteInternalAsync(string tradeId, string buyer, CancellationToken ct)
        {
            TradeDto? trade;

            try { trade = await shopAPIClient.GetTradeAsync(tradeId, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Trade {tradeId} could not be fetched: {e.Message}");
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, e.Message);
            }

            if (trade == null)
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable);

            if (string.Equals(trade.signer, buyer, StringComparison.OrdinalIgnoreCase))
                return new CreditsQuoteResult(CreditsPurchaseError.OwnListing);

            if (trade.received.Length == 0)
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Trade has no received asset");

            TradeAssetDto price = trade.received[0];

            if (price.assetType != CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA
                && price.assetType != CreditsTradeEncoder.ASSET_TYPE_ERC20)
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, $"Trade asset type {price.assetType} cannot be paid with credits");

            ManaUsdRate rate;

            try { rate = await manaUsdRateReader.ReadAsync(trade.contract, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA/USD rate unavailable for trade {tradeId}: {e.Message}");
                return new CreditsQuoteResult(CreditsPurchaseError.PriceUnavailable, e.Message);
            }

            bool isLegacyMana = price.assetType == CreditsTradeEncoder.ASSET_TYPE_ERC20;

            int usdCents = CreditsTradeEncoder.RoundUpToWholeCredit(
                isLegacyMana
                    ? CreditsTradeEncoder.ManaWeiToUsdCents(price.amount, rate)
                    : CreditsTradeEncoder.UsdWeiToCents(price.amount),
                CENTS_PER_CREDIT);

            BigInteger requiredManaWei = isLegacyMana
                ? CreditsTradeEncoder.AmountOrZero(price.amount)
                : CreditsTradeEncoder.UsdWeiToManaWei(price.amount, rate);

            if (usdCents <= 0 || requiredManaWei <= BigInteger.Zero)
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Trade has no price");

            return CreditsQuoteResult.Ok(new CreditsPurchaseQuote(trade, usdCents, usdCents / CENTS_PER_CREDIT, requiredManaWei, isLegacyMana));
        }

        private async UniTask<CreditsPurchaseResult> PurchaseInternalAsync(CreditsPurchaseQuote quote, string buyer, CancellationToken ct)
        {
            SetState(CreditsPurchaseState.Authorizing);

            AuthorizeCreditResponse authorization;

            try { authorization = await creditsAPIClient.AuthorizeUsdCreditAsync(quote.UsdCents, quote.Trade.id, ct); }
            catch (OperationCanceledException) { throw; }
            catch (UnityWebRequestException e)
            {
                bool insufficient = (e.Text ?? string.Empty).IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0
                                    || (e.Text ?? string.Empty).IndexOf("balance", StringComparison.OrdinalIgnoreCase) >= 0;

                return Fail(insufficient ? CreditsPurchaseError.InsufficientCredits : CreditsPurchaseError.AuthorizationFailed, message: e.Text);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return Fail(CreditsPurchaseError.AuthorizationFailed, message: e.Message);
            }

            if (authorization.usdCents > quote.UsdCents)
            {
                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.PriceChanged, message: $"Authorized for {authorization.usdCents} cents, buyer confirmed {quote.UsdCents}");
            }

            string useCreditsCalldata;
            BigInteger authorizedCap;

            try
            {
                authorizedCap = BigInteger.Parse(authorization.maxCreditedValue)
                                + CreditsTradeEncoder.UncreditedValue(authorization.maxCreditedValue, authorization.credit.availableAmount);

                useCreditsCalldata = CreditsTradeEncoder.BuildUseCreditsCalldata(
                    quote.Trade, buyer, authorization.credit, authorization.maxCreditedValue,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() + EXTERNAL_CALL_TTL_SECONDS,
                    RandomSalt());
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.EncodingFailed, message: e.Message);
            }

            if (authorizedCap < quote.RequiredManaWei)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                    $"Authorized cap {authorizedCap} wei cannot cover the {quote.RequiredManaWei} wei trade {quote.Trade.id} draws");

                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.PriceChanged, message: "The authorized credit cannot cover this trade");
            }

            SetState(CreditsPurchaseState.Signing);

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
                case RelayOutcome.Broadcast:
                    txHash = relay.TxHash;
                    break;
                case RelayOutcome.SignatureRejected:
                    await ReleaseIntentAsync(authorization.credit.id);
                    return Fail(CreditsPurchaseError.SignatureRejected, message: relay.Message);
                case RelayOutcome.SigningFailed:
                    await ReleaseIntentAsync(authorization.credit.id);
                    return Fail(CreditsPurchaseError.SigningFailed, message: relay.Message);
                case RelayOutcome.AmbiguousBroadcast:
                    SetState(CreditsPurchaseState.Failed);
                    return new CreditsPurchaseResult(CreditsPurchaseError.SettlementPending, message: relay.Message);
                case RelayOutcome.RelayerRejected:
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Relayer refused trade {quote.Trade.id}: {relay.Message}");
                    await ReleaseIntentAsync(authorization.credit.id);
                    return Fail(CreditsPurchaseError.RelayerUnavailable, message: relay.Message);
            }

            if (string.IsNullOrEmpty(txHash))
            {
                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.RelayerUnavailable, message: "No transaction hash");
            }

            SetState(CreditsPurchaseState.WaitingSettlement);

            SettlementOutcome settlement = await settlementPoller.WaitForSettlementAsync(txHash!, SETTLEMENT_TIMEOUT, ct); // non-null: guarded by the IsNullOrEmpty check above

            switch (settlement)
            {
                case SettlementOutcome.Confirmed:
                    SetState(CreditsPurchaseState.Success);
                    return CreditsPurchaseResult.Ok(txHash!);
                case SettlementOutcome.Reverted:
                    await ReleaseIntentAsync(authorization.credit.id);
                    return Fail(CreditsPurchaseError.TransactionReverted, txHash);
                default:
                    SetState(CreditsPurchaseState.Failed);
                    return new CreditsPurchaseResult(CreditsPurchaseError.SettlementPending, txHash);
            }
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
            SetState(CreditsPurchaseState.Failed);
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
