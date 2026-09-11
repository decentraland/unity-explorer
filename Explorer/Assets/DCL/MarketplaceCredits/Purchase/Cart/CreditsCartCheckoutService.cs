using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.Cart
{
    /// <summary>
    ///     Port of the web shop's cart checkout (Server/shop/app/src/lib/cart-checkout.ts + pages/Cart.tsx charge):
    ///     review every line live, group units by the transaction that settles them, reserve one credit per group,
    ///     then sign and settle the groups one at a time.
    /// </summary>
    public class CreditsCartCheckoutService : ICreditsCartCheckoutService
    {
        private const int CENTS_PER_CREDIT = 10;
        private static readonly TimeSpan RELEASE_INTENT_TIMEOUT = TimeSpan.FromSeconds(15);

        private readonly ShopCart cart;
        private readonly ICreditsPurchaseService quoteService;
        private readonly MarketplaceShopAPIClient shopAPIClient;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly ManaUsdRateReader manaUsdRateReader;
        private readonly CreditsChainConfig chainConfig;
        private readonly IWeb3IdentityCache identityCache;
        private readonly CreditsFeatureAccess creditsFeatureAccess;
        private readonly UseCreditsExecutor executor;
        private readonly bool isFeatureEnabled;
        private readonly CancellationTokenSource lifetimeCts = new ();

        public event Action<CartCheckoutProgress>? StateChanged;
        public event Action<CartCheckoutResult>? CheckoutCompleted;

        public bool IsCheckoutInFlight { get; private set; }

        public CartCheckoutProgress CurrentProgress { get; private set; }

        public CartCheckoutResult? LastResult { get; private set; }

        public CreditsCartCheckoutService(
            ShopCart cart,
            ICreditsPurchaseService quoteService,
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
            this.cart = cart;
            this.quoteService = quoteService;
            this.shopAPIClient = shopAPIClient;
            this.creditsAPIClient = creditsAPIClient;
            this.manaUsdRateReader = manaUsdRateReader;
            this.chainConfig = chainConfig;
            this.identityCache = identityCache;
            this.creditsFeatureAccess = creditsFeatureAccess;
            this.isFeatureEnabled = isFeatureEnabled;
            executor = new UseCreditsExecutor(creditsAPIClient, metaTxRelayer, settlementPoller);
        }

        public void Dispose() =>
            lifetimeCts.SafeCancelAndDispose();

        public void AcknowledgeResult() =>
            LastResult = null;

        public async UniTask<CartReviewResult> ReviewAsync(IReadOnlyList<ShopCartLine> lines, CancellationToken ct)
        {
            if (!isFeatureEnabled || !creditsFeatureAccess.IsUserAllowed())
                return new CartReviewResult(CreditsPurchaseError.FeatureDisabled);

            if (identityCache.Identity == null)
                return new CartReviewResult(CreditsPurchaseError.UnknownError, "No web3 identity");

            SetProgress(new CartCheckoutProgress(CartCheckoutStage.Reviewing, 0, 0, 0, 0));

            var buyable = new List<ReviewedCartLine>(lines.Count);
            var dropped = new List<CartReviewIssue>();
            var groupKeys = new HashSet<string>();
            var totalCredits = 0;
            var totalCents = 0;
            var units = 0;
            var priceChanged = false;

            // Sequential on purpose: deterministic, and a large basket must not hammer the API.
            foreach (ShopCartLine line in lines)
            {
                if (ct.IsCancellationRequested)
                    return new CartReviewResult(CreditsPurchaseError.Cancelled);

                CreditsQuoteResult quote = await QuoteLineAsync(line, ct);

                switch (quote.Error)
                {
                    case CreditsPurchaseError.None:
                        var reviewed = new ReviewedCartLine(line, quote.Quote, line.Quantity);
                        buyable.Add(reviewed);
                        groupKeys.Add(GroupKeyOf(reviewed.Quote, line.Listing));
                        totalCredits += reviewed.TotalCredits;
                        totalCents += reviewed.UnitUsdCents * line.Quantity;
                        units += line.Quantity;
                        priceChanged |= reviewed.PriceChanged;
                        break;
                    case CreditsPurchaseError.OwnListing:
                        dropped.Add(new CartReviewIssue(line, CartLineIssue.OwnListing));
                        break;
                    case CreditsPurchaseError.FeatureDisabled:
                        return new CartReviewResult(CreditsPurchaseError.FeatureDisabled);
                    case CreditsPurchaseError.Cancelled:
                        return new CartReviewResult(CreditsPurchaseError.Cancelled);
                    default:
                        dropped.Add(new CartReviewIssue(line, CartLineIssue.Unavailable));
                        break;
                }
            }

            bool orderChanged = dropped.Count > 0 || priceChanged;
            return CartReviewResult.Ok(new CartReview(buyable, dropped, totalCredits, totalCents, units, groupKeys.Count, orderChanged, DateTime.UtcNow));
        }

        public async UniTask<CartCheckoutResult> CheckoutAsync(CartReview review, CancellationToken uiCt)
        {
            if (!isFeatureEnabled || !creditsFeatureAccess.IsUserAllowed())
                return Finish(EarlyFailure(review, CartCheckoutOutcome.Failed, CreditsPurchaseError.FeatureDisabled, null));

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return Finish(EarlyFailure(review, CartCheckoutOutcome.Failed, CreditsPurchaseError.UnknownError, "No web3 identity"));

            if (review.Buyable.Count == 0)
                return Finish(EarlyFailure(review, CartCheckoutOutcome.Failed, CreditsPurchaseError.ListingNotAvailable, "Nothing in the cart can be bought"));

            if (IsCheckoutInFlight)
                return Finish(EarlyFailure(review, CartCheckoutOutcome.Failed, CreditsPurchaseError.UnknownError, "A checkout is already in progress"));

            IsCheckoutInFlight = true;

            try { return Finish(await CheckoutInternalAsync(review, identity.Address, uiCt)); }
            catch (OperationCanceledException)
            {
                return Finish(EarlyFailure(review, CartCheckoutOutcome.Cancelled, CreditsPurchaseError.Cancelled, null));
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return Finish(EarlyFailure(review, CartCheckoutOutcome.Failed, CreditsPurchaseError.UnknownError, e.Message));
            }
            finally { IsCheckoutInFlight = false; }
        }

        private async UniTask<CreditsQuoteResult> QuoteLineAsync(ShopCartLine line, CancellationToken ct)
        {
            ShopListingDto listing = line.Listing;

            if (!listing.IsStoreMint())
                return await quoteService.QuoteAsync(listing, ct);

            if (string.IsNullOrEmpty(listing.itemId))
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "Mint listing has no itemId");

            // A mint's price and supply live on chain and move without any listing changing: re-read before quoting.
            ShopListingDto? fresh;

            try { fresh = await shopAPIClient.GetShopListingForItemAsync(listing.contractAddress, listing.itemId!, ct); }
            catch (OperationCanceledException) { return new CreditsQuoteResult(CreditsPurchaseError.Cancelled); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Mint {line.Id} could not be re-read for the cart review: {e.Message}");
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, e.Message);
            }

            if (fresh == null || !fresh.IsStoreMint() || fresh.available < line.Quantity || string.IsNullOrEmpty(fresh.manaWei))
                return new CreditsQuoteResult(CreditsPurchaseError.ListingNotAvailable, "The mint is no longer available");

            return await quoteService.QuoteAsync(fresh, ct);
        }

        private async UniTask<CartCheckoutResult> CheckoutInternalAsync(CartReview review, string buyer, CancellationToken uiCt)
        {
            List<CheckoutGroup> groups = GroupUnits(review);
            int unitCount = review.UnitCount;

            foreach (CheckoutGroup group in groups)
            {
                if (group.ChainId != chainConfig.ChainId)
                    return EarlyFailure(review, CartCheckoutOutcome.Failed, CreditsPurchaseError.ListingNotAvailable, $"Group {group.Key} is not on chain {chainConfig.ChainId}");
            }

            // Phase A: reserve every group before signing anything, so a shortfall on the last group never leaves
            // the buyer with a half-bought cart.
            var unitsReserved = 0;

            for (var i = 0; i < groups.Count; i++)
            {
                CheckoutGroup group = groups[i];
                SetProgress(new CartCheckoutProgress(CartCheckoutStage.Reserving, i + 1, groups.Count, unitsReserved, unitCount));

                if (uiCt.IsCancellationRequested)
                {
                    await ReleaseGroupsAsync(groups, 0);
                    return BuildResult(groups, CartCheckoutOutcome.Cancelled, CreditsPurchaseError.Cancelled, null, -1, CartCheckoutStage.Reserving);
                }

                CreditsPurchaseError prepareError = await PrepareGroupAsync(group, uiCt);

                if (prepareError != CreditsPurchaseError.None)
                {
                    await ReleaseGroupsAsync(groups, 0);
                    group.Error = prepareError;
                    return BuildResult(groups, prepareError == CreditsPurchaseError.Cancelled ? CartCheckoutOutcome.Cancelled : CartCheckoutOutcome.Failed, prepareError, group.Message, -1, CartCheckoutStage.Reserving);
                }

                EnumResult<AuthorizeGroupResponse, CreditsAuthorizeError> authorization = await creditsAPIClient.AuthorizeUsdCreditGroupAsync(group.BuildCheckoutLines(), uiCt);

                if (!authorization.Success)
                {
                    await ReleaseGroupsAsync(groups, 0);
                    (CreditsAuthorizeError state, string message) = authorization.Error!.Value;
                    return AuthorizationFailure(groups, group, state, message);
                }

                AuthorizeGroupResponse response = authorization.Value;
                group.Credit = response.credit;
                group.MaxCreditedValue = response.maxCreditedValue;
                group.Reserved = true;

                if (response.usdCents > group.UsdCents)
                {
                    await ReleaseGroupsAsync(groups, 0);
                    group.Error = CreditsPurchaseError.PriceChanged;
                    return BuildResult(groups, CartCheckoutOutcome.Failed, CreditsPurchaseError.PriceChanged,
                        $"Authorized for {response.usdCents} cents, buyer confirmed {group.UsdCents}", -1, CartCheckoutStage.Reserving);
                }

                BigInteger authorizedCap = BigInteger.Parse(response.maxCreditedValue)
                                           + CreditsTradeEncoder.UncreditedValue(response.maxCreditedValue, response.credit.availableAmount);

                if (authorizedCap < group.RequiredManaWei)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                        $"Authorized cap {authorizedCap} wei cannot cover the {group.RequiredManaWei} wei group {group.Key} draws");

                    await ReleaseGroupsAsync(groups, 0);
                    group.Error = CreditsPurchaseError.PriceChanged;
                    return BuildResult(groups, CartCheckoutOutcome.Failed, CreditsPurchaseError.PriceChanged, "The authorized credits cannot cover this purchase", -1, CartCheckoutStage.Reserving);
                }

                unitsReserved += group.Units.Count;
            }

            // Phase B: one signature per group, settled before the next is signed. From here on the buyer's
            // cancellation is ignored: a signed group runs to a terminal state on the service lifetime.
            BigInteger nextMinNonce = BigInteger.MinusOne;
            CreditsPurchaseError firstError = CreditsPurchaseError.None;
            string? firstMessage = null;
            CartCheckoutStage failedAt = CartCheckoutStage.Completed;

            for (var i = 0; i < groups.Count; i++)
            {
                CheckoutGroup group = groups[i];
                int groupIndex = i + 1;

                if (uiCt.IsCancellationRequested)
                {
                    await ReleaseGroupsAsync(groups, i);
                    firstError = CreditsPurchaseError.Cancelled;
                    failedAt = CartCheckoutStage.Signing;
                    break;
                }

                UseCreditsRequest request;

                try { request = group.BuildRequest(buyer, chainConfig.CollectionStoreAddress, nextMinNonce); }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                    await ReleaseGroupsAsync(groups, i);
                    group.Error = CreditsPurchaseError.EncodingFailed;
                    firstError = CreditsPurchaseError.EncodingFailed;
                    firstMessage = e.Message;
                    failedAt = CartCheckoutStage.Signing;
                    break;
                }

                SetProgress(new CartCheckoutProgress(CartCheckoutStage.Signing, groupIndex, groups.Count, unitCount, unitCount));

                UseCreditsOutcome outcome = await executor.ExecuteAsync(request,
                    state => OnExecutorStateChanged(state, groupIndex, groups.Count, unitCount),
                    lifetimeCts.Token);

                group.Outcome = outcome;

                if (outcome.Settled)
                {
                    nextMinNonce = outcome.Nonce.Sign >= 0 ? outcome.Nonce + BigInteger.One : BigInteger.MinusOne;
                    continue;
                }

                // A rejected, reverted or still-pending group ends the checkout: the executor already dealt with its
                // credit, and a later group would race the nonce of a pending one. Never-signed groups are released.
                firstError = outcome.Error;
                firstMessage = outcome.Message;
                failedAt = outcome.Broadcast ? CartCheckoutStage.WaitingSettlement : CartCheckoutStage.Signing;
                await ReleaseGroupsAsync(groups, i + 1);
                break;
            }

            var settledCount = 0;

            foreach (CheckoutGroup group in groups)
            {
                if (group.Outcome is { Settled: true })
                    settledCount++;
            }

            CartCheckoutOutcome checkoutOutcome;

            if (settledCount == groups.Count)
                checkoutOutcome = CartCheckoutOutcome.Completed;
            else if (settledCount > 0)
                checkoutOutcome = CartCheckoutOutcome.PartiallyCompleted;
            else
                checkoutOutcome = firstError is CreditsPurchaseError.Cancelled or CreditsPurchaseError.SignatureRejected
                    ? CartCheckoutOutcome.Cancelled
                    : CartCheckoutOutcome.Failed;

            return BuildResult(groups, checkoutOutcome, firstError, firstMessage, -1, failedAt);
        }

        private static List<CheckoutGroup> GroupUnits(CartReview review)
        {
            var groups = new List<CheckoutGroup>();
            var indexByKey = new Dictionary<string, int>();

            foreach (ReviewedCartLine line in review.Buyable)
            {
                string key = GroupKeyOf(line.Quote, line.Line.Listing);

                if (!indexByKey.TryGetValue(key, out int index))
                {
                    index = groups.Count;
                    indexByKey[key] = index;
                    int chainId = line.Quote.Kind == CreditsListingKind.StoreMint ? line.Line.Listing.chainId : line.Quote.Trade!.chainId;
                    groups.Add(new CheckoutGroup(key, line.Quote.Kind, chainId));
                }

                CheckoutGroup group = groups[index];

                for (var i = 0; i < line.Quantity; i++)
                    group.Units.Add(line);
            }

            return groups;
        }

        // Web `purchaseGroupKey`: what gets authorized together is exactly what gets submitted together.
        private static string GroupKeyOf(in CreditsPurchaseQuote quote, ShopListingDto listing) =>
            quote.Kind == CreditsListingKind.StoreMint
                ? $"store:{listing.chainId}"
                : $"trade:{quote.Trade!.chainId}:{quote.Trade!.contract.ToLowerInvariant()}";

        /// <summary>
        ///     Resolves what the group will draw: mints are re-read live (CollectionStore re-validates the price, a
        ///     stale one reverts), USD-pegged trades need the oracle to know their MANA, legacy trades carry it.
        /// </summary>
        private async UniTask<CreditsPurchaseError> PrepareGroupAsync(CheckoutGroup group, CancellationToken ct)
        {
            group.UsdCents = 0;
            group.RequiredManaWei = BigInteger.Zero;

            if (group.Kind == CreditsListingKind.StoreMint)
            {
                var liveByLine = new Dictionary<string, ShopListingDto>();
                var unitsByLine = new Dictionary<string, int>();

                foreach (ReviewedCartLine unit in group.Units)
                    unitsByLine[unit.Line.Id] = unitsByLine.TryGetValue(unit.Line.Id, out int count) ? count + 1 : 1;

                foreach (ReviewedCartLine unit in group.Units)
                {
                    ShopListingDto listing = unit.Line.Listing;
                    group.UsdCents += unit.UnitUsdCents;

                    if (!liveByLine.TryGetValue(unit.Line.Id, out ShopListingDto? fresh))
                    {
                        try { fresh = await shopAPIClient.GetShopListingForItemAsync(listing.contractAddress, listing.itemId ?? string.Empty, ct); }
                        catch (OperationCanceledException) { return CreditsPurchaseError.Cancelled; }
                        catch (Exception e)
                        {
                            group.Message = e.Message;
                            return CreditsPurchaseError.ListingNotAvailable;
                        }

                        if (fresh == null || !fresh.IsStoreMint() || fresh.available < unitsByLine[unit.Line.Id] || string.IsNullOrEmpty(fresh.manaWei))
                        {
                            group.Message = "The mint is no longer available";
                            return CreditsPurchaseError.ListingNotAvailable;
                        }

                        liveByLine[unit.Line.Id] = fresh;
                    }

                    BigInteger livePrice = CreditsTradeEncoder.AmountOrZero(fresh.manaWei);

                    if (livePrice <= BigInteger.Zero)
                    {
                        group.Message = "The mint has no price";
                        return CreditsPurchaseError.ListingNotAvailable;
                    }

                    if (livePrice > unit.Quote.RequiredManaWei)
                    {
                        group.Message = "The mint price changed";
                        return CreditsPurchaseError.PriceChanged;
                    }

                    group.Mints.Add(new StoreMintTarget(listing.contractAddress, listing.itemId ?? string.Empty, fresh.manaWei!));
                    group.RequiredManaWei += livePrice;
                }

                return CreditsPurchaseError.None;
            }

            foreach (ReviewedCartLine unit in group.Units)
            {
                TradeDto trade = unit.Quote.Trade!;
                group.UsdCents += unit.UnitUsdCents;
                group.Trades.Add(trade);

                BigInteger required;

                if (unit.Quote.IsLiveRatePrice)
                    required = unit.Quote.RequiredManaWei;
                else
                {
                    ManaUsdRate rate;

                    try { rate = await manaUsdRateReader.ReadAsync(trade.contract, ct); }
                    catch (OperationCanceledException) { return CreditsPurchaseError.Cancelled; }
                    catch (Exception e)
                    {
                        ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA/USD rate unavailable at checkout for trade {trade.id}: {e.Message}");
                        group.Message = e.Message;
                        return CreditsPurchaseError.PriceUnavailable;
                    }

                    required = CreditsTradeEncoder.UsdWeiToManaWei(trade.received[0].amount, rate);
                }

                if (required <= BigInteger.Zero)
                {
                    group.Message = "Trade draws no MANA";
                    return CreditsPurchaseError.PriceUnavailable;
                }

                group.RequiredManaWei += required;
            }

            return CreditsPurchaseError.None;
        }

        private CartCheckoutResult AuthorizationFailure(List<CheckoutGroup> groups, CheckoutGroup group, CreditsAuthorizeError state, string message)
        {
            switch (state)
            {
                case CreditsAuthorizeError.Cancelled:
                    group.Error = CreditsPurchaseError.Cancelled;
                    return BuildResult(groups, CartCheckoutOutcome.Cancelled, CreditsPurchaseError.Cancelled, null, -1, CartCheckoutStage.Reserving);
                case CreditsAuthorizeError.InsufficientCredits:
                    group.Error = CreditsPurchaseError.InsufficientCredits;
                    return BuildResult(groups, CartCheckoutOutcome.InsufficientCredits, CreditsPurchaseError.InsufficientCredits, message, ParseMissingCredits(message), CartCheckoutStage.Reserving);
                case CreditsAuthorizeError.FeatureDisabled:
                    group.Error = CreditsPurchaseError.FeatureDisabled;
                    return BuildResult(groups, CartCheckoutOutcome.Failed, CreditsPurchaseError.FeatureDisabled, message, -1, CartCheckoutStage.Reserving);
                default:
                    group.Error = CreditsPurchaseError.AuthorizationFailed;
                    return BuildResult(groups, CartCheckoutOutcome.Failed, CreditsPurchaseError.AuthorizationFailed, message, -1, CartCheckoutStage.Reserving);
            }
        }

        // The 402 body carries { balanceCents, requiredCents }; anything else reads as "unknown".
        internal static int ParseMissingCredits(string? body)
        {
            if (string.IsNullOrEmpty(body))
                return -1;

            try
            {
                JObject json = JObject.Parse(body);
                long? required = json["requiredCents"]?.Value<long>();
                long? balance = json["balanceCents"]?.Value<long>();

                if (required == null || balance == null)
                    return -1;

                long missingCents = Math.Max(0, required.Value - balance.Value);
                return (int)((missingCents + CENTS_PER_CREDIT - 1) / CENTS_PER_CREDIT);
            }
            catch (Exception) { return -1; }
        }

        private void OnExecutorStateChanged(CreditsPurchaseState state, int groupIndex, int groupCount, int unitCount)
        {
            switch (state)
            {
                case CreditsPurchaseState.Signing:
                    SetProgress(new CartCheckoutProgress(CartCheckoutStage.Signing, groupIndex, groupCount, unitCount, unitCount));
                    break;
                case CreditsPurchaseState.WaitingSettlement:
                    SetProgress(new CartCheckoutProgress(CartCheckoutStage.WaitingSettlement, groupIndex, groupCount, unitCount, unitCount));
                    break;
            }
        }

        /// <summary>Releases the credits of every reserved, never-signed group from the given index on.</summary>
        private async UniTask ReleaseGroupsAsync(List<CheckoutGroup> groups, int fromIndex)
        {
            var salts = new List<string>();

            for (int i = fromIndex; i < groups.Count; i++)
            {
                CheckoutGroup group = groups[i];

                if (!group.Reserved || group.Released || group.Outcome != null)
                    continue;

                group.Released = true;
                salts.Add(group.Credit.id);
            }

            if (salts.Count == 0)
                return;

            using var timeoutCts = new CancellationTokenSource(RELEASE_INTENT_TIMEOUT);

            try { await creditsAPIClient.ReleaseUsdIntentsAsync(salts.ToArray(), timeoutCts.Token); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Failed to release {salts.Count} cart credit intents: {e.Message}");
            }
        }

        private CartCheckoutResult BuildResult(List<CheckoutGroup> groups, CartCheckoutOutcome outcome, CreditsPurchaseError firstError, string? message, int missingCredits, CartCheckoutStage failedAt)
        {
            var groupOutcomes = new List<CartGroupOutcome>(groups.Count);
            var boughtLineIds = new List<string>();
            var boughtIds = new HashSet<string>();
            var boughtUnits = new List<ReviewedCartLine>();
            var unboughtUnits = new List<ReviewedCartLine>();
            var settledHashes = new List<string>();
            var pending = false;

            foreach (CheckoutGroup group in groups)
            {
                UseCreditsOutcome? executed = group.Outcome;
                bool settled = executed is { Settled: true };
                bool broadcast = executed is { Broadcast: true };
                bool reverted = executed is { Reverted: true };
                pending |= broadcast && !settled && !reverted;

                CreditsPurchaseError error = executed?.Error ?? (group.Error != CreditsPurchaseError.None ? group.Error : CreditsPurchaseError.Cancelled);

                groupOutcomes.Add(new CartGroupOutcome(group.Key, group.Kind, group.Units.Count, group.UsdCents, group.Reserved ? group.Credit.id : null,
                    executed?.TxHash, broadcast, settled, reverted, error, executed?.Message ?? group.Message));

                if (settled && executed!.Value.TxHash != null)
                    settledHashes.Add(executed.Value.TxHash!);

                foreach (ReviewedCartLine unit in group.Units)
                {
                    if (settled)
                    {
                        boughtUnits.Add(unit);

                        if (boughtIds.Add(unit.Line.Id))
                            boughtLineIds.Add(unit.Line.Id);
                    }
                    else
                        unboughtUnits.Add(unit);
                }
            }

            if (boughtLineIds.Count > 0)
                cart.RemoveAll(boughtLineIds);

            return new CartCheckoutResult(outcome, groupOutcomes, boughtLineIds, boughtUnits, unboughtUnits, settledHashes, pending, firstError, message, missingCredits, failedAt);
        }

        private static CartCheckoutResult EarlyFailure(CartReview review, CartCheckoutOutcome outcome, CreditsPurchaseError error, string? message) =>
            new (outcome, Array.Empty<CartGroupOutcome>(), Array.Empty<string>(), Array.Empty<ReviewedCartLine>(), review.Buyable, Array.Empty<string>(),
                false, error, message, -1, CartCheckoutStage.Reserving);

        private CartCheckoutResult Finish(CartCheckoutResult result)
        {
            LastResult = result;
            bool bought = result.Outcome is CartCheckoutOutcome.Completed or CartCheckoutOutcome.PartiallyCompleted;
            SetProgress(new CartCheckoutProgress(bought ? CartCheckoutStage.Completed : CartCheckoutStage.Failed, 0, result.Groups.Count, 0, 0));
            CheckoutCompleted?.Invoke(result);
            return result;
        }

        private void SetProgress(in CartCheckoutProgress progress)
        {
            CurrentProgress = progress;
            StateChanged?.Invoke(progress);
        }

        /// <summary>The units that settle in one transaction, with the credit reserved for them.</summary>
        private sealed class CheckoutGroup
        {
            public readonly string Key;
            public readonly CreditsListingKind Kind;
            public readonly int ChainId;
            public readonly List<ReviewedCartLine> Units = new ();
            public readonly List<TradeDto> Trades = new ();
            public readonly List<StoreMintTarget> Mints = new ();

            public int UsdCents;
            public BigInteger RequiredManaWei;
            public AuthorizedCredit Credit;
            public string MaxCreditedValue = string.Empty;
            public bool Reserved;
            public bool Released;
            public UseCreditsOutcome? Outcome;
            public CreditsPurchaseError Error;
            public string? Message;

            public CheckoutGroup(string key, CreditsListingKind kind, int chainId)
            {
                Key = key;
                Kind = kind;
                ChainId = chainId;
            }

            // One CheckoutLine per unit (web checkoutLineFor): the price, the trade when there is one, and the item
            // pair whenever it is known so the purchase history can name the line.
            public IReadOnlyList<CheckoutLine> BuildCheckoutLines()
            {
                var lines = new CheckoutLine[Units.Count];

                for (var i = 0; i < Units.Count; i++)
                {
                    ReviewedCartLine unit = Units[i];
                    ShopListingDto listing = unit.Line.Listing;
                    bool hasItem = !string.IsNullOrEmpty(listing.contractAddress) && !string.IsNullOrEmpty(listing.itemId);

                    lines[i] = new CheckoutLine(
                        unit.UnitUsdCents,
                        unit.Quote.Kind == CreditsListingKind.Trade ? unit.Quote.Trade!.id : null,
                        hasItem ? listing.contractAddress : null,
                        hasItem ? listing.itemId : null);
                }

                return lines;
            }

            public UseCreditsRequest BuildRequest(string buyer, string collectionStoreAddress, BigInteger minNonce)
            {
                string target;
                byte[] selector;
                byte[] data;

                if (Kind == CreditsListingKind.StoreMint)
                {
                    target = collectionStoreAddress;
                    (selector, data) = CreditsTradeEncoder.BuildStoreBuyCall(Mints, buyer);
                }
                else
                {
                    target = Trades[0].contract;
                    (selector, data) = CreditsTradeEncoder.BuildAcceptCall(Trades, buyer);
                }

                return new UseCreditsRequest(buyer, $"cart group {Key}", target, selector, data, Credit, MaxCreditedValue, RequiredManaWei, minNonce);
            }
        }
    }
}
