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
        private readonly ManaRail manaRail;
        private readonly PaymentOptionsProvider paymentOptionsProvider;
        private readonly bool isFeatureEnabled;

        public event Action<CreditsPurchaseState>? StateChanged;

        public CreditsPurchaseService(
            MarketplaceShopAPIClient shopAPIClient,
            MarketplaceCreditsAPIClient creditsAPIClient,
            UseCreditsExecutor executor,
            ManaRail manaRail,
            PaymentOptionsProvider paymentOptionsProvider,
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
            this.executor = executor;
            this.manaRail = manaRail;
            this.paymentOptionsProvider = paymentOptionsProvider;
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

        public async UniTask<PaymentOptions> GetPaymentOptionsAsync(CreditsPurchaseQuote quote, CancellationToken ct)
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (!isFeatureEnabled || !creditsFeatureAccess.IsUserAllowed() || identity == null)
                return PaymentOptions.NONE;

            try
            {
                BigInteger priceManaWei = await paymentOptionsProvider.ManaPriceOfAsync(quote, ct);
                return await paymentOptionsProvider.ComputeAsync(identity.Address, quote.UsdCents, priceManaWei, ct);
            }
            catch (OperationCanceledException) { return PaymentOptions.NONE; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return PaymentOptions.NONE;
            }
        }

        public async UniTask<CreditsPurchaseResult> PurchaseAsync(CreditsPurchaseQuote quote, PaymentOption option, CancellationToken ct)
        {
            if (!isFeatureEnabled || !creditsFeatureAccess.IsUserAllowed())
                return new CreditsPurchaseResult(CreditsPurchaseError.FeatureDisabled);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return new CreditsPurchaseResult(CreditsPurchaseError.UnknownError, message: "No web3 identity");

            try { return await PurchaseInternalAsync(quote, option, identity.Address, ct); }
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

        private async UniTask<CreditsPurchaseResult> PurchaseInternalAsync(CreditsPurchaseQuote quote, PaymentOption option, string buyer, CancellationToken ct)
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

            return option.Method switch
            {
                PaymentMethod.Mana => await PurchaseWithManaAsync(quote, mint, option, requiredManaWei, buyer, ct),
                PaymentMethod.CreditsAndMana => await PurchaseWithCreditsAndManaAsync(quote, mint, option, requiredManaWei, buyer, ct),
                _ => await PurchaseWithCreditsAsync(quote, mint, requiredManaWei, buyer, ct),
            };
        }

        private async UniTask<CreditsPurchaseResult> PurchaseWithCreditsAsync(CreditsPurchaseQuote quote, StoreMintTarget mint, BigInteger requiredManaWei, string buyer, CancellationToken ct)
        {
            SetState(CreditsPurchaseState.Authorizing);

            AuthorizeCreditResponse authorization;

            try { authorization = await creditsAPIClient.AuthorizeUsdCreditAsync(quote.UsdCents, quote.TradeId, quote.ContractAddress, quote.ItemId, ct); }
            catch (OperationCanceledException) { throw; }
            catch (UnityWebRequestException e)
            {
                return Fail(IsInsufficientCredits(e) ? CreditsPurchaseError.InsufficientCredits : CreditsPurchaseError.AuthorizationFailed, message: e.Text);
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

            ExternalCall call;

            try { call = BuildExternalCall(quote, mint, buyer); }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await executor.ReleaseIntentAsync(authorization.credit.id);
                return Fail(CreditsPurchaseError.EncodingFailed, message: e.Message);
            }

            var request = new UseCreditsRequest(buyer, QuoteLabel(quote), call.Target, call.Selector, call.Data,
                authorization.credit, authorization.maxCreditedValue, requiredManaWei, BigInteger.MinusOne);

            return ToResult(await executor.ExecuteAsync(request, SetState, ct, ct));
        }

        /// <summary>
        ///     Credits first, MANA for the rest: the credit is sized to the credits leg, and the gap is added to
        ///     maxCreditedValue so that useCredits pulls it from the buyer's MANA.
        /// </summary>
        private async UniTask<CreditsPurchaseResult> PurchaseWithCreditsAndManaAsync(CreditsPurchaseQuote quote, StoreMintTarget mint, PaymentOption option, BigInteger requiredManaWei, string buyer,
            CancellationToken ct)
        {
            if (option.CreditsCents <= 0 || option.CreditsCents >= quote.UsdCents)
                return Fail(CreditsPurchaseError.PriceChanged, message: $"A credits leg of {option.CreditsCents} cents does not split a price of {quote.UsdCents}");

            ExternalCall call;

            try { call = BuildExternalCall(quote, mint, buyer); }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return Fail(CreditsPurchaseError.EncodingFailed, message: e.Message);
            }

            SetState(CreditsPurchaseState.Authorizing);

            AuthorizeCreditResponse authorization;

            try { authorization = await creditsAPIClient.AuthorizeUsdCreditAsync(option.CreditsCents, quote.TradeId, quote.ContractAddress, quote.ItemId, ct); }
            catch (OperationCanceledException) { throw; }
            catch (UnityWebRequestException e)
            {
                return Fail(IsInsufficientCredits(e) ? CreditsPurchaseError.InsufficientCredits : CreditsPurchaseError.AuthorizationFailed, message: e.Text);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return Fail(CreditsPurchaseError.AuthorizationFailed, message: e.Message);
            }

            string creditId = authorization.credit.id;

            if (authorization.usdCents > option.CreditsCents)
            {
                await executor.ReleaseIntentAsync(creditId);
                return Fail(CreditsPurchaseError.PriceChanged, message: $"Authorized for {authorization.usdCents} cents, buyer confirmed {option.CreditsCents}");
            }

            // The gap is the buyer's share of the price, raised when the credit turned out to be worth less MANA
            // than its share, so that the credit plus the gap always reach what the call draws.
            BigInteger creditValueWei = BigInteger.Parse(authorization.credit.availableAmount);
            BigInteger gapWei = PaymentOptionsCalculator.ManaForRemainder(quote.UsdCents - option.CreditsCents, quote.UsdCents, requiredManaWei);

            if (creditValueWei + gapWei < requiredManaWei)
                gapWei = requiredManaWei - creditValueWei;

            if (PaymentOptionsCalculator.ExceedsConfirmedMana(gapWei, option.ManaWei))
            {
                await executor.ReleaseIntentAsync(creditId);
                return Fail(CreditsPurchaseError.PriceChanged, message: $"The purchase needs {gapWei} MANA wei, buyer confirmed {option.ManaWei}");
            }

            CreditsPurchaseError fundsError;

            try { fundsError = await PrepareManaAsync(buyer, chainConfig.CreditsManagerAddress, gapWei, ct); }
            catch (OperationCanceledException)
            {
                await executor.ReleaseIntentAsync(creditId);
                throw;
            }

            if (fundsError != CreditsPurchaseError.None)
            {
                await executor.ReleaseIntentAsync(creditId);
                return Fail(fundsError);
            }

            var request = new UseCreditsRequest(buyer, QuoteLabel(quote), call.Target, call.Selector, call.Data,
                authorization.credit, (creditValueWei + gapWei).ToString(), requiredManaWei, BigInteger.MinusOne);

            return ToResult(await executor.ExecuteAsync(request, SetState, ct, ct));
        }

        /// <summary>
        ///     No credits: the marketplace (trade) or the CollectionStore (mint) is called directly and pulls the
        ///     price from the buyer's MANA, so nothing is reserved on the credits-server.
        /// </summary>
        private async UniTask<CreditsPurchaseResult> PurchaseWithManaAsync(CreditsPurchaseQuote quote, StoreMintTarget mint, PaymentOption option, BigInteger requiredManaWei, string buyer, CancellationToken ct)
        {
            if (PaymentOptionsCalculator.ExceedsConfirmedMana(requiredManaWei, option.ManaWei))
                return Fail(CreditsPurchaseError.PriceChanged, message: $"The purchase needs {requiredManaWei} MANA wei, buyer confirmed {option.ManaWei}");

            MetaTxContract target;

            if (quote.Kind == CreditsListingKind.StoreMint)
                target = chainConfig.CollectionStoreContract;
            else if (chainConfig.IsKnownMarketplace(quote.Trade!.contract))
                target = chainConfig.MarketplaceContract(quote.Trade!.contract);
            else
                return Fail(CreditsPurchaseError.ListingNotAvailable, message: $"Trade {quote.Trade!.id} names an unknown marketplace {quote.Trade!.contract}");

            string calldata;

            try
            {
                ExternalCall call = BuildExternalCall(quote, mint, buyer);
                calldata = CreditsTradeEncoder.ToCalldata(call.Selector, call.Data);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return Fail(CreditsPurchaseError.EncodingFailed, message: e.Message);
            }

            CreditsPurchaseError fundsError = await PrepareManaAsync(buyer, target.Address, requiredManaWei, ct);

            if (fundsError != CreditsPurchaseError.None)
                return Fail(fundsError);

            var request = new ManaPurchaseRequest(buyer, QuoteLabel(quote), target, calldata, BigInteger.MinusOne);
            return ToResult(await manaRail.ExecuteAsync(request, SetState, ct, ct));
        }

        /// <summary>
        ///     Checks the buyer holds the MANA and that the spender may pull it, asking for the approval when it may
        ///     not. A balance that cannot be read is not treated as a shortfall.
        /// </summary>
        private async UniTask<CreditsPurchaseError> PrepareManaAsync(string buyer, string spender, BigInteger manaWei, CancellationToken ct)
        {
            BigInteger? balance = await paymentOptionsProvider.ManaBalanceAsync(buyer, ct);

            if (balance < manaWei)
                return CreditsPurchaseError.InsufficientMana;

            SetState(CreditsPurchaseState.ApprovingMana);

            ManaApprovalResult approval = await manaRail.EnsureAllowanceAsync(buyer, spender, manaWei, BigInteger.MinusOne, ct, ct);
            return approval.Error;
        }

        private ExternalCall BuildExternalCall(in CreditsPurchaseQuote quote, in StoreMintTarget mint, string buyer)
        {
            if (quote.Kind == CreditsListingKind.StoreMint)
            {
                (byte[] mintSelector, byte[] mintData) = CreditsTradeEncoder.BuildStoreBuyCall(mint.CollectionAddress, mint.ItemId, mint.PriceWei, buyer);
                return new ExternalCall(chainConfig.CollectionStoreAddress, mintSelector, mintData);
            }

            (byte[] selector, byte[] data) = CreditsTradeEncoder.BuildAcceptCall(quote.Trade!, buyer);
            return new ExternalCall(quote.Trade!.contract, selector, data);
        }

        private static bool IsInsufficientCredits(UnityWebRequestException e)
        {
            string text = e.Text ?? string.Empty;

            return text.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("balance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static CreditsPurchaseResult ToResult(in PurchaseOutcome outcome) =>
            outcome.Success
                ? CreditsPurchaseResult.Ok(outcome.TxHash!)
                : new CreditsPurchaseResult(outcome.Error, outcome.TxHash, outcome.Message);

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

        private readonly struct ExternalCall
        {
            public readonly string Target;
            public readonly byte[] Selector;
            public readonly byte[] Data;

            public ExternalCall(string target, byte[] selector, byte[] data)
            {
                Target = target;
                Selector = selector;
                Data = data;
            }
        }
    }
}
