using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using System;
using System.Numerics;
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

        private readonly MarketplaceShopAPIClient shopAPIClient;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly ManaUsdRateReader manaUsdRateReader;
        private readonly CreditsChainConfig chainConfig;
        private readonly IWeb3IdentityCache identityCache;
        private readonly CreditsFeatureAccess creditsFeatureAccess;
        private readonly UseCreditsExecutor executor;
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
            this.manaUsdRateReader = manaUsdRateReader;
            this.chainConfig = chainConfig;
            this.identityCache = identityCache;
            this.creditsFeatureAccess = creditsFeatureAccess;
            this.isFeatureEnabled = isFeatureEnabled;
            executor = new UseCreditsExecutor(creditsAPIClient, metaTxRelayer, settlementPoller);
        }

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
                return listing.IsStoreMint()
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

        private async UniTask<CreditsQuoteResult> QuoteStoreMintInternalAsync(ShopListingDto listing, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(listing.itemId))
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Mint listing has no itemId");

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

                if (fresh == null || !fresh.IsStoreMint() || fresh.available <= 0 || string.IsNullOrEmpty(fresh.manaWei))
                    return Fail(CreditsPurchaseError.ListingNotAvailable, message: "The mint is no longer available");

                mint = new StoreMintTarget(mint.CollectionAddress, mint.ItemId, fresh.manaWei!);
                requiredManaWei = CreditsTradeEncoder.AmountOrZero(fresh.manaWei!);

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
                await executor.ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.PriceChanged, message: $"Authorized for {authorization.usdCents} cents, buyer confirmed {quote.UsdCents}");
            }

            string externalCallTarget;
            byte[] externalCallSelector;
            byte[] externalCallData;

            try
            {
                if (quote.Kind == CreditsListingKind.StoreMint)
                {
                    externalCallTarget = chainConfig.CollectionStoreAddress;
                    (externalCallSelector, externalCallData) = CreditsTradeEncoder.BuildStoreBuyCall(mint.CollectionAddress, mint.ItemId, mint.PriceWei, buyer);
                }
                else
                {
                    externalCallTarget = quote.Trade!.contract;
                    (externalCallSelector, externalCallData) = CreditsTradeEncoder.BuildAcceptCall(quote.Trade!, buyer);
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await executor.ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.EncodingFailed, message: e.Message);
            }

            var request = new UseCreditsRequest(buyer, QuoteLabel(quote), externalCallTarget, externalCallSelector, externalCallData,
                authorization.credit, authorization.maxCreditedValue, requiredManaWei, BigInteger.MinusOne);

            UseCreditsOutcome outcome = await executor.ExecuteAsync(request, SetState, ct);

            return outcome.Success
                ? CreditsPurchaseResult.Ok(outcome.TxHash!)
                : new CreditsPurchaseResult(outcome.Error, outcome.TxHash, outcome.Message);
        }

        private static string QuoteLabel(in CreditsPurchaseQuote quote) =>
            quote.Kind == CreditsListingKind.StoreMint
                ? $"mint {quote.Mint.CollectionAddress}-{quote.Mint.ItemId}"
                : $"trade {quote.Trade!.id}";

        private CreditsPurchaseResult Fail(CreditsPurchaseError error, string? txHash = null, string? message = null)
        {
            SetState(CreditsPurchaseState.Failed);
            return new CreditsPurchaseResult(error, txHash, message);
        }

        private void SetState(CreditsPurchaseState state) =>
            StateChanged?.Invoke(state);
    }
}
