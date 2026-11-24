using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using DCL.Web3.Identities;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Numerics;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.Names
{
    /// <summary>
    ///     Claims a NAME with credits, alone or topped up with the buyer's Polygon MANA. Port of the shop web
    ///     app's lib/names.ts registerNameWithUsdCredits: the payment goes through CreditsManager.useCredits on
    ///     Polygon, whose external call is a route the credits-server signs, and a bridge then registers the
    ///     NAME on Ethereum for the signed-in address.
    /// </summary>
    public class NameClaimService : IDisposable
    {
        private const string NAME_PRICE_MANA_WEI = "100000000000000000000";
        private const string ETH_CALL_METHOD = "eth_call";
        private const string LATEST_BLOCK = "latest";
        private const string ROUTE_COST_TOO_HIGH_CODE = "ROUTE_COST_TOO_HIGH";
        private const string NAME_NOT_AVAILABLE_TEXT = "not available";
        private const int WORD_HEX_LENGTH = 64;

        private static readonly BigInteger NAME_PRICE = BigInteger.Parse(NAME_PRICE_MANA_WEI);

        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly IEthereumApi ethereumApi;
        private readonly ManaUsdRateReader manaUsdRateReader;
        private readonly AcrossBridgeStatusPoller bridgeStatusPoller;
        private readonly CreditsChainConfig chainConfig;
        private readonly IWeb3IdentityCache identityCache;
        private readonly CreditsFeatureAccess creditsFeatureAccess;
        private readonly UseCreditsExecutor executor;
        private readonly ManaRail manaRail;
        private readonly PaymentOptionsProvider paymentOptionsProvider;
        private readonly RelayedPurchaseGate purchaseGate;
        private readonly bool isFeatureEnabled;
        private readonly CancellationTokenSource disposalCts = new ();

        /// <summary>True when the signed-in user can pay a NAME with credits from the client.</summary>
        public virtual bool IsAvailable => isFeatureEnabled && creditsFeatureAccess.IsUserAllowed();

        /// <summary>The NAME being claimed, null when no claim is running. One claim runs at a time.</summary>
        public string? InFlightName { get; private set; }

        public NameClaimStage CurrentStage { get; private set; }

        /// <summary>The NAME of the last finished claim whose result nobody acknowledged yet, null when there is none.</summary>
        public string? LastResultName { get; private set; }

        public NameClaimResult LastResult { get; private set; }

        public virtual event Action<NameClaimStage>? StageChanged;

        /// <summary>Raised once per claim with its final result, also when the caller stopped waiting for it.</summary>
        public virtual event Action<string, NameClaimResult>? Completed;

        public NameClaimService(
            MarketplaceCreditsAPIClient creditsAPIClient,
            IEthereumApi ethereumApi,
            UseCreditsExecutor executor,
            ManaRail manaRail,
            PaymentOptionsProvider paymentOptionsProvider,
            RelayedPurchaseGate purchaseGate,
            ManaUsdRateReader manaUsdRateReader,
            AcrossBridgeStatusPoller bridgeStatusPoller,
            CreditsChainConfig chainConfig,
            IWeb3IdentityCache identityCache,
            CreditsFeatureAccess creditsFeatureAccess,
            bool isFeatureEnabled)
        {
            this.creditsAPIClient = creditsAPIClient;
            this.ethereumApi = ethereumApi;
            this.manaUsdRateReader = manaUsdRateReader;
            this.bridgeStatusPoller = bridgeStatusPoller;
            this.chainConfig = chainConfig;
            this.identityCache = identityCache;
            this.creditsFeatureAccess = creditsFeatureAccess;
            this.isFeatureEnabled = isFeatureEnabled;
            this.executor = executor;
            this.manaRail = manaRail;
            this.paymentOptionsProvider = paymentOptionsProvider;
            this.purchaseGate = purchaseGate;
        }

        public void Dispose() =>
            disposalCts.SafeCancelAndDispose();

        public void AcknowledgeResult() =>
            LastResultName = null;

        /// <summary>
        ///     Reads DCLRegistrar.available(name) on Ethereum. Never throws: an unreadable registrar is Unknown.
        /// </summary>
        public virtual async UniTask<NameAvailability> CheckAvailabilityAsync(string name, CancellationToken ct)
        {
            try
            {
                var request = new EthApiRequest
                {
                    readonlyNetwork = chainConfig.EthereumReadonlyNetwork,
                    id = Guid.NewGuid().GetHashCode(),
                    method = ETH_CALL_METHOD,
                    @params = new object[]
                    {
                        new JObject
                        {
                            ["to"] = chainConfig.DclRegistrarAddress,
                            ["data"] = CreditsTradeEncoder.BuildNameAvailableCalldata(name),
                        },
                        LATEST_BLOCK,
                    },
                };

                EthApiResponse response = await ethereumApi.SendAsync(request, Web3RequestSource.Internal, ct);
                return DecodeAvailability(response.result?.ToString());
            }
            catch (OperationCanceledException) { return NameAvailability.Unknown; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"NAME availability could not be read for {name}: {e.Message}");
                return NameAvailability.Unknown;
            }
        }

        /// <summary>
        ///     The fixed 100 MANA price in credits at the live MANA/USD rate, rounded up to a whole credit.
        /// </summary>
        public virtual async UniTask<NameQuoteResult> QuoteAsync(CancellationToken ct)
        {
            if (!IsAvailable)
                return new NameQuoteResult(NameClaimError.FeatureDisabled);

            try
            {
                ManaUsdRate rate = await manaUsdRateReader.ReadAsync(chainConfig.OffChainMarketplaceAddress, ct);
                int usdCents = CreditsTradeEncoder.RoundUpToWholeCredit(CreditsTradeEncoder.ManaWeiToUsdCents(NAME_PRICE_MANA_WEI, rate), PaymentOptionsCalculator.CENTS_PER_CREDIT);

                return usdCents > 0
                    ? new NameQuoteResult(new NameClaimQuote(usdCents, usdCents / PaymentOptionsCalculator.CENTS_PER_CREDIT))
                    : new NameQuoteResult(NameClaimError.PriceUnavailable);
            }
            catch (OperationCanceledException) { return new NameQuoteResult(NameClaimError.Cancelled); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA/USD rate unavailable for the NAME price: {e.Message}");
                return new NameQuoteResult(NameClaimError.PriceUnavailable);
            }
        }

        /// <summary>
        ///     How the signed-in buyer can pay the quoted price: with credits, or with credits topped up with
        ///     MANA. Never throws: a balance that cannot be read pays for nothing.
        /// </summary>
        public virtual async UniTask<PaymentOptions> GetPaymentOptionsAsync(NameClaimQuote quote, CancellationToken ct)
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (!IsAvailable || identity == null)
                return PaymentOptions.NONE;

            // useCredits reverts without a credit, so MANA is never offered as a method of its own.
            try { return await paymentOptionsProvider.ComputeAsync(identity.Address, quote.UsdCents, NAME_PRICE, canManaPayAlone: false, ct); }
            catch (OperationCanceledException) { return PaymentOptions.NONE; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return PaymentOptions.NONE;
            }
        }

        /// <summary>
        ///     Runs the claim to a final result and never throws. <paramref name="option" /> is the payment the
        ///     buyer confirmed: credits, or credits plus MANA. Cancelling <paramref name="ct" /> stops the
        ///     claim only before the payment is signed, releasing the reserved credit; from the signature on the
        ///     payment and the bridge are followed to the end on the service's own lifetime. Every result but a
        ///     cancellation is kept in <see cref="LastResult" /> until it is acknowledged.
        /// </summary>
        public virtual async UniTask<NameClaimResult> ClaimAsync(string name, NameClaimQuote quote, PaymentOption option, CancellationToken ct)
        {
            if (InFlightName != null)
                return NameClaimResult.Failed(NameClaimError.UnknownError, "A claim is already in progress");

            using RelayedPurchaseGate.Lease? paymentLease = purchaseGate.TryEnter();

            if (paymentLease == null)
                return NameClaimResult.Failed(NameClaimError.PurchaseInProgress);

            InFlightName = name;
            LastResultName = null;

            NameClaimResult result;

            try { result = await ClaimInternalAsync(name, quote, option, paymentLease, ct); }
            catch (OperationCanceledException) { result = NameClaimResult.Failed(NameClaimError.Cancelled); }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                result = NameClaimResult.Failed(NameClaimError.UnknownError, e.Message);
            }

            InFlightName = null;

            if (result.Error != NameClaimError.Cancelled)
            {
                LastResult = result;
                LastResultName = name;
            }

            Completed?.Invoke(name, result);
            return result;
        }

        internal static NameAvailability DecodeAvailability(string? resultHex)
        {
            if (string.IsNullOrEmpty(resultHex))
                return NameAvailability.Unknown;

            int start = resultHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 2 : 0;

            if (resultHex.Length < start + WORD_HEX_LENGTH)
                return NameAvailability.Unknown;

            // A bool is one word whose last digit is 1 or 0.
            return resultHex[start + WORD_HEX_LENGTH - 1] == '1' ? NameAvailability.Available : NameAvailability.Taken;
        }

        internal static NameClaimError MapRouteError(long responseCode, string? responseText)
        {
            string text = responseText ?? string.Empty;

            return responseCode switch
                   {
                       (long)HttpStatusCode.NotFound => NameClaimError.FeatureDisabled,
                       (long)HttpStatusCode.BadRequest when text.IndexOf(NAME_NOT_AVAILABLE_TEXT, StringComparison.OrdinalIgnoreCase) >= 0 => NameClaimError.NameUnavailable,
                       (long)HttpStatusCode.ServiceUnavailable when text.IndexOf(ROUTE_COST_TOO_HIGH_CODE, StringComparison.Ordinal) >= 0 => NameClaimError.RouteTooExpensive,
                       _ => NameClaimError.RouteUnavailable,
                   };
        }

        private async UniTask<NameClaimResult> ClaimInternalAsync(string name, NameClaimQuote quote, PaymentOption option, RelayedPurchaseGate.Lease paymentLease, CancellationToken ct)
        {
            if (!IsAvailable)
                return NameClaimResult.Failed(NameClaimError.FeatureDisabled);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return NameClaimResult.Failed(NameClaimError.UnknownError, "No web3 identity");

            if (NameRules.Validate(name) != NameValidation.Valid)
                return NameClaimResult.Failed(NameClaimError.NameUnavailable, "Invalid name");

            if (option.Method == PaymentMethod.Mana)
                return NameClaimResult.Failed(NameClaimError.UnknownError, "A NAME cannot be paid with MANA alone");

            bool isMixed = option.Method == PaymentMethod.CreditsAndMana;

            if (isMixed && (option.CreditsCents <= 0 || option.CreditsCents >= quote.UsdCents))
                return NameClaimResult.Failed(NameClaimError.PriceChanged, $"A credits leg of {option.CreditsCents} cents does not split a price of {quote.UsdCents}");

            int creditsCents = isMixed ? option.CreditsCents : quote.UsdCents;

            SetStage(NameClaimStage.Preparing);

            NameCreditRouteResponse route;

            try { route = await creditsAPIClient.GetNameRouteAsync(name, chainConfig.ChainId, ct); }
            catch (UnityWebRequestException e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"NAME route refused for {name}: {e.ResponseCode} {e.Text}");
                return NameClaimResult.Failed(MapRouteError(e.ResponseCode, e.Text), e.Text);
            }

            AuthorizeCreditResponse authorization;

            // On the service lifetime: a credit reserved while the buyer was closing the modal is still known here.
            try { authorization = await creditsAPIClient.AuthorizeUsdNameCreditAsync(creditsCents, name, disposalCts.Token); }
            catch (UnityWebRequestException e)
            {
                return NameClaimResult.Failed(
                    e.ResponseCode == (long)HttpStatusCode.PaymentRequired ? NameClaimError.InsufficientCredits : NameClaimError.AuthorizationFailed,
                    e.Text);
            }

            if (ct.IsCancellationRequested)
            {
                await executor.ReleaseIntentAsync(authorization.credit.id);
                return NameClaimResult.Failed(NameClaimError.Cancelled);
            }

            BigInteger creditCapWei;
            BigInteger manaGapWei;

            try
            {
                creditCapWei = BigInteger.Parse(authorization.maxCreditedValue);

                // What the credit leaves of the price is drawn from the buyer's own MANA.
                manaGapWei = CreditsTradeEncoder.UncreditedValue(NAME_PRICE_MANA_WEI, authorization.credit.availableAmount);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await executor.ReleaseIntentAsync(authorization.credit.id);
                return NameClaimResult.Failed(NameClaimError.AuthorizationFailed, e.Message);
            }

            // A credits claim confirmed no MANA, so its credit must cover the whole price, by its cap and by what is left of it.
            if (authorization.usdCents > creditsCents || (!isMixed && (creditCapWei < NAME_PRICE || manaGapWei.Sign > 0)))
            {
                await executor.ReleaseIntentAsync(authorization.credit.id);
                return NameClaimResult.Failed(NameClaimError.PriceChanged, $"Authorized {authorization.usdCents} cents for {authorization.maxCreditedValue} wei, {authorization.credit.availableAmount} wei available");
            }

            if (isMixed)
            {
                ManaApprovalResult funds;

                try
                {
                    funds = await manaRail.PrepareAsync(identity.Address, new[] { new ManaSpend(chainConfig.CreditsManagerAddress, manaGapWei) }, option.ManaWei,
                        OnPaymentStateChanged, ct, disposalCts.Token);
                }
                catch (Exception)
                {
                    await executor.ReleaseIntentAsync(authorization.credit.id);
                    throw;
                }

                if (!funds.Success)
                {
                    await executor.ReleaseIntentAsync(authorization.credit.id);
                    return NameClaimResult.Failed(MapPaymentError(funds.Error), funds.Message);
                }
            }

            UseCreditsRequest request;

            try
            {
                NameRouteExternalCall call = route.externalCall;
                var signedCall = new SignedExternalCall(call.expiresAt, call.salt.HexToByteArray(), route.customExternalCallSignature.HexToByteArray());

                request = new UseCreditsRequest(identity.Address, $"name {name}", call.target, call.selector.HexToByteArray(), call.data.HexToByteArray(),
                    authorization.credit, NAME_PRICE_MANA_WEI, NAME_PRICE, BigInteger.MinusOne, signedCall);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await executor.ReleaseIntentAsync(authorization.credit.id);
                return NameClaimResult.Failed(NameClaimError.RouteUnavailable, e.Message);
            }

            PurchaseOutcome outcome = await executor.ExecuteAsync(request, OnPaymentStateChanged, ct, disposalCts.Token);

            // The bridge wait that follows relays nothing and runs for minutes.
            paymentLease.Dispose();

            // Without a transaction hash there is nothing to look the bridge deposit up by.
            if (outcome.Error == CreditsPurchaseError.SettlementPending && string.IsNullOrEmpty(outcome.TxHash))
                return new NameClaimResult(NameClaimStatus.Pending, message: outcome.Message);

            if (!outcome.Success && outcome.Error != CreditsPurchaseError.SettlementPending)
                return NameClaimResult.Failed(MapPaymentError(outcome.Error), outcome.Message);

            SetStage(NameClaimStage.Registering);

            // The credits are spent from here on: the bridge is followed on the service's own lifetime.
            AcrossFillOutcome fill = await bridgeStatusPoller.WaitForFillAsync(outcome.TxHash!, disposalCts.Token);

            return fill switch
                   {
                       AcrossFillOutcome.Registered => new NameClaimResult(NameClaimStatus.Registered),
                       AcrossFillOutcome.NotRegistered => NameClaimResult.Failed(NameClaimError.NotRegistered),
                       _ => new NameClaimResult(NameClaimStatus.Pending),
                   };
        }

        private static NameClaimError MapPaymentError(CreditsPurchaseError error) =>
            error switch
            {
                CreditsPurchaseError.PriceChanged => NameClaimError.PriceChanged,
                CreditsPurchaseError.SignatureRejected => NameClaimError.SignatureRejected,
                CreditsPurchaseError.SigningFailed => NameClaimError.SigningFailed,
                CreditsPurchaseError.SignatureTimedOut => NameClaimError.SignatureTimedOut,
                CreditsPurchaseError.RelayerUnavailable => NameClaimError.RelayerUnavailable,
                CreditsPurchaseError.TransactionReverted => NameClaimError.TransactionReverted,
                CreditsPurchaseError.InsufficientMana => NameClaimError.InsufficientMana,
                CreditsPurchaseError.ManaApprovalFailed => NameClaimError.ManaApprovalFailed,
                _ => NameClaimError.UnknownError,
            };

        private void OnPaymentStateChanged(CreditsPurchaseState state)
        {
            switch (state)
            {
                case CreditsPurchaseState.ApprovingMana:
                    SetStage(NameClaimStage.ApprovingMana);
                    break;
                case CreditsPurchaseState.Signing:
                    SetStage(NameClaimStage.Signing);
                    break;
                case CreditsPurchaseState.WaitingSettlement:
                    SetStage(NameClaimStage.Confirming);
                    break;
            }
        }

        private void SetStage(NameClaimStage stage)
        {
            CurrentStage = stage;
            StageChanged?.Invoke(stage);
        }
    }
}
