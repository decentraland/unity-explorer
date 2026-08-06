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
        private readonly CreditsChainConfig chainConfig;
        private readonly IWeb3IdentityCache identityCache;
        private readonly CreditsFeatureAccess creditsFeatureAccess;
        private readonly bool isFeatureEnabled;

        public event Action<CreditsPurchaseState>? StateChanged;

        public CreditsPurchaseService(
            MarketplaceShopAPIClient shopAPIClient,
            MarketplaceCreditsAPIClient creditsAPIClient,
            CreditsManagerMetaTxRelayer metaTxRelayer,
            PolygonSettlementPoller settlementPoller,
            ManaUsdRateReader manaUsdRateReader,
            CreditsChainConfig chainConfig,
            IWeb3IdentityCache identityCache,
            CreditsFeatureAccess creditsFeatureAccess,
            bool isFeatureEnabled)
        {
            this.shopAPIClient = shopAPIClient;
            this.creditsAPIClient = creditsAPIClient;
            this.metaTxRelayer = metaTxRelayer;
            this.settlementPoller = settlementPoller;
            this.manaUsdRateReader = manaUsdRateReader;
            this.chainConfig = chainConfig;
            this.identityCache = identityCache;
            this.creditsFeatureAccess = creditsFeatureAccess;
            this.isFeatureEnabled = isFeatureEnabled;
        }

        /// <summary>
        ///     Quotes the LISTING, not a trade id.
        ///     <para>
        ///         The row from /v3/catalog/unified already says how the item is acquired, what it costs and how
        ///         much is left; passing only its tradeId threw that away and forced everything to be re-derived
        ///         from a trade — which a CollectionStore mint does not have, so it could not be bought at all.
        ///     </para>
        /// </summary>
        public async UniTask<CreditsQuoteResult> QuoteAsync(ShopListingDto listing, CancellationToken ct)
        {
            if (!isFeatureEnabled || !creditsFeatureAccess.IsUserAllowed())
                return new CreditsQuoteResult(CreditsPurchaseError.FeatureDisabled);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return new CreditsQuoteResult(CreditsPurchaseError.UnknownError, "No web3 identity");

            SetState(CreditsPurchaseState.ResolvingListing);

            try
            {
                return IsStoreMint(listing)
                    ? await QuoteStoreMintInternalAsync(listing, ct)
                    : await QuoteInternalAsync(listing.tradeId, identity.Address, ct);
            }
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
            if (!isFeatureEnabled || !creditsFeatureAccess.IsUserAllowed())
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

        /// <summary>
        ///     A CollectionStore mint is FOR SALE when it has stock, not when it has a trade.
        ///     <para>
        ///         `acquisition` is the server's own discriminator. A missing value means an older server that did
        ///         not send it, and back then every row was a trade — so the absence must read as "trade", never
        ///         as "mint".
        ///     </para>
        /// </summary>
        private static bool IsStoreMint(ShopListingDto listing) =>
            string.Equals(listing.acquisition, "store", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Quotes a mint entirely from the listing row: there is no trade to fetch and never will be.
        ///     <para>
        ///         A mint is always MANA-priced, so its credit price exists only through the oracle — the same
        ///         path a legacy MANA trade takes. The rate is read through the marketplace contract because that
        ///         is what exposes `manaUsdAggregator()`; a collection contract does not.
        ///     </para>
        /// </summary>
        private async UniTask<CreditsQuoteResult> QuoteStoreMintInternalAsync(ShopListingDto listing, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(listing.itemId))
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Mint listing has no itemId");

            // Stock, not a trade, is what "still on sale" means here. A sold-out mint would otherwise be carried
            // all the way to an on-chain revert.
            if (listing.available <= 0)
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Mint is sold out");

            if (string.IsNullOrEmpty(listing.manaWei))
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Mint listing has no price");

            ManaUsdRate rate;

            try { rate = await manaUsdRateReader.ReadAsync(chainConfig.OffChainMarketplaceAddress, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                    $"MANA/USD rate unavailable for mint {listing.contractAddress}-{listing.itemId}: {e.Message}");

                return new CreditsQuoteResult(CreditsPurchaseError.PriceUnavailable, e.Message);
            }

            int usdCents = CreditsTradeEncoder.RoundUpToWholeCredit(CreditsTradeEncoder.ManaWeiToUsdCents(listing.manaWei!, rate), CENTS_PER_CREDIT);
            BigInteger requiredManaWei = CreditsTradeEncoder.AmountOrZero(listing.manaWei!);

            if (usdCents <= 0 || requiredManaWei <= BigInteger.Zero)
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Mint has no price");

            var target = new StoreMintTarget(listing.contractAddress, listing.itemId!, listing.manaWei!);
            return CreditsQuoteResult.Ok(CreditsPurchaseQuote.ForMint(target, usdCents, usdCents / CENTS_PER_CREDIT, requiredManaWei));
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

            bool isLegacyMana = price.assetType == CreditsTradeEncoder.ASSET_TYPE_ERC20;

            int usdCents;
            BigInteger requiredManaWei;

            if (isLegacyMana)
            {
                // Only a MANA-denominated price needs the oracle to be displayed at all: its credit price
                // exists only through the rate.
                ManaUsdRate rate;

                try { rate = await manaUsdRateReader.ReadAsync(trade.contract, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA/USD rate unavailable for trade {tradeId}: {e.Message}");
                    return new CreditsQuoteResult(CreditsPurchaseError.PriceUnavailable, e.Message);
                }

                usdCents = CreditsTradeEncoder.RoundUpToWholeCredit(CreditsTradeEncoder.ManaWeiToUsdCents(price.amount, rate), CENTS_PER_CREDIT);
                requiredManaWei = CreditsTradeEncoder.AmountOrZero(price.amount);

                if (requiredManaWei <= BigInteger.Zero)
                    return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Trade has no price");
            }
            else
            {
                // A USD-pegged price is exact without the oracle; the MANA the trade draws is resolved at
                // purchase time, so only warm the rate cache for the confirm click.
                usdCents = CreditsTradeEncoder.RoundUpToWholeCredit(CreditsTradeEncoder.UsdWeiToCents(price.amount), CENTS_PER_CREDIT);
                requiredManaWei = BigInteger.Zero;

                manaUsdRateReader.PrefetchAsync(trade.contract).Forget();
            }

            if (usdCents <= 0)
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Trade has no price");

            return CreditsQuoteResult.Ok(CreditsPurchaseQuote.ForTrade(trade, usdCents, usdCents / CENTS_PER_CREDIT, requiredManaWei, isLegacyMana));
        }

        private async UniTask<CreditsPurchaseResult> PurchaseInternalAsync(CreditsPurchaseQuote quote, string buyer, CancellationToken ct)
        {
            BigInteger requiredManaWei = quote.RequiredManaWei;
            StoreMintTarget mint = quote.Mint;

            // A mint's price is an ARGUMENT to CollectionStore.buy and the contract re-validates it against the
            // item's live price, reverting if the creator moved it — so it is re-read here, as late as possible,
            // instead of trusting the quote. A trade cannot fail this way: its price is signed into the order.
            if (quote.Kind == CreditsListingKind.StoreMint)
            {
                SetState(CreditsPurchaseState.ResolvingListing);

                ShopListingDto? fresh;

                try { fresh = await shopAPIClient.GetShopListingForItemAsync(mint.CollectionAddress, mint.ItemId, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                        $"Mint {mint.CollectionAddress}-{mint.ItemId} could not be re-read before buying: {e.Message}");

                    return Fail(CreditsPurchaseError.ListingNotAvailable, message: e.Message);
                }

                if (fresh == null || !IsStoreMint(fresh) || fresh.available <= 0 || string.IsNullOrEmpty(fresh.manaWei))
                    return Fail(CreditsPurchaseError.ListingNotAvailable, message: "The mint is no longer available");

                mint = new StoreMintTarget(mint.CollectionAddress, mint.ItemId, fresh.manaWei!);
                requiredManaWei = CreditsTradeEncoder.AmountOrZero(fresh.manaWei!);

                // A price that moved UP past what the buyer confirmed must not be charged silently. The cap check
                // below catches it too, but failing here names the reason.
                if (requiredManaWei > quote.RequiredManaWei)
                    return Fail(CreditsPurchaseError.PriceChanged, message: "The mint price changed");
            }
            else if (!quote.IsLiveRatePrice)
            {
                SetState(CreditsPurchaseState.ResolvingListing);

                ManaUsdRate rate;

                try { rate = await manaUsdRateReader.ReadAsync(quote.Trade!.contract, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA/USD rate unavailable at purchase for trade {quote.Trade!.id}: {e.Message}");
                    return Fail(CreditsPurchaseError.PriceUnavailable, message: e.Message);
                }

                requiredManaWei = CreditsTradeEncoder.UsdWeiToManaWei(quote.Trade!.received[0].amount, rate);

                if (requiredManaWei <= BigInteger.Zero)
                    return Fail(CreditsPurchaseError.PriceUnavailable, message: "Trade draws no MANA");
            }

            SetState(CreditsPurchaseState.Authorizing);

            AuthorizeCreditResponse authorization;

            try { authorization = await creditsAPIClient.AuthorizeUsdCreditAsync(quote.UsdCents, quote.TradeId, quote.ContractAddress, quote.ItemId, ct); }
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

                long externalCallExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + EXTERNAL_CALL_TTL_SECONDS;

                // Same envelope, same credit, same caps — only the external call differs: accept([trade]) against
                // the marketplace, or buy([item]) against the CollectionStore.
                useCreditsCalldata = quote.Kind == CreditsListingKind.StoreMint
                    ? CreditsTradeEncoder.BuildStoreMintUseCreditsCalldata(
                        chainConfig.CollectionStoreAddress, mint.CollectionAddress, mint.ItemId, mint.PriceWei,
                        buyer, authorization.credit, authorization.maxCreditedValue,
                        externalCallExpiresAt, RandomSalt())
                    : CreditsTradeEncoder.BuildUseCreditsCalldata(
                        quote.Trade!, buyer, authorization.credit, authorization.maxCreditedValue,
                        externalCallExpiresAt, RandomSalt());
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.EncodingFailed, message: e.Message);
            }

            if (authorizedCap < requiredManaWei)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                    $"Authorized cap {authorizedCap} wei cannot cover the {requiredManaWei} wei {QuoteLabel(quote)} draws");

                await ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.PriceChanged, message: "The authorized credit cannot cover this purchase");
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
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Relayer refused {QuoteLabel(quote)}: {relay.Message}");
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

        /// <summary>What the logs should call this purchase — a mint has no trade id to name.</summary>
        private static string QuoteLabel(in CreditsPurchaseQuote quote) =>
            quote.Kind == CreditsListingKind.StoreMint
                ? $"mint {quote.Mint.CollectionAddress}-{quote.Mint.ItemId}"
                : $"trade {quote.Trade!.id}";

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
