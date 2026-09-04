using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     One CreditsManager.useCredits call, from an authorized credit to a settled receipt. The external call
    ///     (accept or buy) arrives already encoded; this owns the envelope, the MANA cap check, the signature, the
    ///     relay, the settlement wait and the intent release rules, so the single-item and cart flows cannot drift
    ///     on the part that moves the money.
    /// </summary>
    public readonly struct UseCreditsRequest
    {
        public readonly string Buyer;
        public readonly string Label;
        public readonly string ExternalCallTarget;
        public readonly byte[] ExternalCallSelector;
        public readonly byte[] ExternalCallData;
        public readonly AuthorizedCredit Credit;
        public readonly string MaxCreditedValue;
        public readonly BigInteger RequiredManaWei;
        public readonly BigInteger MinNonce;

        public UseCreditsRequest(
            string buyer,
            string label,
            string externalCallTarget,
            byte[] externalCallSelector,
            byte[] externalCallData,
            AuthorizedCredit credit,
            string maxCreditedValue,
            BigInteger requiredManaWei,
            BigInteger minNonce)
        {
            Buyer = buyer;
            Label = label;
            ExternalCallTarget = externalCallTarget;
            ExternalCallSelector = externalCallSelector;
            ExternalCallData = externalCallData;
            Credit = credit;
            MaxCreditedValue = maxCreditedValue;
            RequiredManaWei = requiredManaWei;
            MinNonce = minNonce;
        }
    }

    public readonly struct UseCreditsOutcome
    {
        public readonly CreditsPurchaseError Error;
        public readonly string? TxHash;
        public readonly string? Message;
        public readonly bool Broadcast;
        public readonly bool Settled;
        public readonly bool Reverted;
        public readonly BigInteger Nonce;

        public bool Success => Error == CreditsPurchaseError.None;

        public UseCreditsOutcome(CreditsPurchaseError error, string? txHash, string? message, bool broadcast, bool settled, bool reverted, BigInteger nonce)
        {
            Error = error;
            TxHash = txHash;
            Message = message;
            Broadcast = broadcast;
            Settled = settled;
            Reverted = reverted;
            Nonce = nonce;
        }

        public static UseCreditsOutcome Failure(CreditsPurchaseError error, string? message = null) =>
            new (error, null, message, false, false, false, BigInteger.MinusOne);
    }

    public class UseCreditsExecutor
    {
        private const long EXTERNAL_CALL_TTL_SECONDS = 60 * 60 * 24;
        private static readonly TimeSpan SETTLEMENT_TIMEOUT = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan RELEASE_INTENT_TIMEOUT = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan REPORT_SUBMISSION_TIMEOUT = TimeSpan.FromSeconds(15);

        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly CreditsManagerMetaTxRelayer metaTxRelayer;
        private readonly PolygonSettlementPoller settlementPoller;

        public UseCreditsExecutor(MarketplaceCreditsAPIClient creditsAPIClient, CreditsManagerMetaTxRelayer metaTxRelayer, PolygonSettlementPoller settlementPoller)
        {
            this.creditsAPIClient = creditsAPIClient;
            this.metaTxRelayer = metaTxRelayer;
            this.settlementPoller = settlementPoller;
        }

        /// <summary>
        ///     Runs the call to a terminal outcome. The credit is released on every failure that provably never
        ///     reached the chain and KEPT on any possible broadcast (double-spend risk); the server reconciler
        ///     settles those. Cancellation during signing releases the intent and rethrows.
        /// </summary>
        public async UniTask<UseCreditsOutcome> ExecuteAsync(UseCreditsRequest request, Action<CreditsPurchaseState> setState, CancellationToken ct)
        {
            AuthorizedCredit credit = request.Credit;
            string useCreditsCalldata;
            BigInteger authorizedCap;

            try
            {
                authorizedCap = BigInteger.Parse(request.MaxCreditedValue)
                                + CreditsTradeEncoder.UncreditedValue(request.MaxCreditedValue, credit.availableAmount);

                long externalCallExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + EXTERNAL_CALL_TTL_SECONDS;

                useCreditsCalldata = CreditsTradeEncoder.BuildUseCreditsCalldata(
                    request.ExternalCallTarget, request.ExternalCallSelector, request.ExternalCallData,
                    credit, request.MaxCreditedValue, externalCallExpiresAt, RandomSalt());
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                await ReleaseIntentAsync(credit.id);
                return Fail(setState, CreditsPurchaseError.EncodingFailed, e.Message);
            }

            if (authorizedCap < request.RequiredManaWei)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                    $"Authorized cap {authorizedCap} wei cannot cover the {request.RequiredManaWei} wei {request.Label} draws");

                await ReleaseIntentAsync(credit.id);
                return Fail(setState, CreditsPurchaseError.PriceChanged, "The authorized credit cannot cover this purchase");
            }

            setState(CreditsPurchaseState.Signing);

            RelayResult relay;

            try
            {
                relay = request.MinNonce.Sign < 0
                    ? await metaTxRelayer.RelayUseCreditsAsync(request.Buyer, useCreditsCalldata, ct)
                    : await metaTxRelayer.RelayUseCreditsAsync(request.Buyer, useCreditsCalldata, request.MinNonce, ct);
            }
            catch (OperationCanceledException)
            {
                await ReleaseIntentAsync(credit.id);
                throw;
            }

            switch (relay.Outcome)
            {
                case RelayOutcome.SignatureRejected:
                    await ReleaseIntentAsync(credit.id);
                    return Fail(setState, CreditsPurchaseError.SignatureRejected, relay.Message);
                case RelayOutcome.SigningFailed:
                    await ReleaseIntentAsync(credit.id);
                    return Fail(setState, CreditsPurchaseError.SigningFailed, relay.Message);
                case RelayOutcome.AmbiguousBroadcast:
                    setState(CreditsPurchaseState.Failed);
                    return new UseCreditsOutcome(CreditsPurchaseError.SettlementPending, null, relay.Message, true, false, false, relay.Nonce);
                case RelayOutcome.RelayerRejected:
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Relayer refused {request.Label}: {relay.Message}");
                    await ReleaseIntentAsync(credit.id);
                    return Fail(setState, CreditsPurchaseError.RelayerUnavailable, relay.Message);
            }

            if (string.IsNullOrEmpty(relay.TxHash))
            {
                await ReleaseIntentAsync(credit.id);
                return Fail(setState, CreditsPurchaseError.RelayerUnavailable, "No transaction hash");
            }

            string txHash = relay.TxHash!;
            ReportSubmissionAsync(credit.id, txHash).Forget();

            setState(CreditsPurchaseState.WaitingSettlement);

            SettlementOutcome settlement = await settlementPoller.WaitForSettlementAsync(txHash, SETTLEMENT_TIMEOUT, ct);

            switch (settlement)
            {
                case SettlementOutcome.Confirmed:
                    setState(CreditsPurchaseState.Success);
                    return new UseCreditsOutcome(CreditsPurchaseError.None, txHash, null, true, true, false, relay.Nonce);
                case SettlementOutcome.Reverted:
                    await ReleaseIntentAsync(credit.id);
                    setState(CreditsPurchaseState.Failed);
                    return new UseCreditsOutcome(CreditsPurchaseError.TransactionReverted, txHash, null, true, false, true, relay.Nonce);
                default:
                    setState(CreditsPurchaseState.Failed);
                    return new UseCreditsOutcome(CreditsPurchaseError.SettlementPending, txHash, null, true, false, false, relay.Nonce);
            }
        }

        public async UniTask ReleaseIntentAsync(string creditId)
        {
            using var timeoutCts = new CancellationTokenSource(RELEASE_INTENT_TIMEOUT);

            try { await creditsAPIClient.ReleaseUsdIntentsAsync(new[] { creditId }, timeoutCts.Token); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Failed to release credit intent {creditId}: {e.Message}");
            }
        }

        private static UseCreditsOutcome Fail(Action<CreditsPurchaseState> setState, CreditsPurchaseError error, string? message)
        {
            setState(CreditsPurchaseState.Failed);
            return UseCreditsOutcome.Failure(error, message);
        }

        private async UniTaskVoid ReportSubmissionAsync(string creditId, string txHash)
        {
            using var timeoutCts = new CancellationTokenSource(REPORT_SUBMISSION_TIMEOUT);

            try { await creditsAPIClient.ReportIntentSubmissionAsync(new[] { creditId }, txHash, timeoutCts.Token); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Failed to report the submission of credit {creditId}: {e.Message}");
            }
        }

        private static byte[] RandomSalt()
        {
            var salt = new byte[32];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            return salt;
        }
    }
}
