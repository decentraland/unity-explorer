using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     The MANA side of a purchase: granting a contract the allowance to pull the buyer's MANA, and settling a
    ///     purchase that spends no credits directly against the marketplace or the CollectionStore.
    /// </summary>
    public class ManaRail
    {
        private static readonly TimeSpan SETTLEMENT_TIMEOUT = TimeSpan.FromSeconds(120);

        private readonly ManaTokenReader manaTokenReader;
        private readonly MetaTxRelayer metaTxRelayer;
        private readonly PolygonSettlementPoller settlementPoller;
        private readonly MetaTxContract manaTokenContract;

        public ManaRail(ManaTokenReader manaTokenReader, MetaTxRelayer metaTxRelayer, PolygonSettlementPoller settlementPoller, CreditsChainConfig chainConfig)
        {
            this.manaTokenReader = manaTokenReader;
            this.metaTxRelayer = metaTxRelayer;
            this.settlementPoller = settlementPoller;
            manaTokenContract = chainConfig.ManaTokenContract;
        }

        /// <summary>
        ///     Everything the MANA of a payment needs before its purchase is signed: the total stays within what
        ///     the buyer confirmed, the buyer holds it, and every spender may pull its share, asking for the
        ///     approvals that are missing. A balance that cannot be read is not treated as a shortfall.
        ///     setState receives <see cref="CreditsPurchaseState.ApprovingMana" /> before each approval request and
        ///     <see cref="CreditsPurchaseState.ConfirmingManaApproval" /> once it is relayed.
        ///     <paramref name="signCt" /> is rethrown when cancelled before a signature.
        /// </summary>
        public async UniTask<ManaApprovalResult> PrepareAsync(string buyer, IReadOnlyList<ManaSpend> spends, BigInteger confirmedWei, Action<CreditsPurchaseState> setState,
            CancellationToken signCt, CancellationToken settleCt)
        {
            BigInteger totalWei = BigInteger.Zero;

            foreach (ManaSpend spend in spends)
                totalWei += spend.Wei;

            if (PaymentOptionsCalculator.ExceedsConfirmedMana(totalWei, confirmedWei))
                return new ManaApprovalResult(CreditsPurchaseError.PriceChanged, $"The payment needs {totalWei} MANA wei, buyer confirmed {confirmedWei}", BigInteger.MinusOne);

            BigInteger? balance = await manaTokenReader.TryBalanceOfAsync(buyer, signCt);

            if (balance < totalWei)
                return new ManaApprovalResult(CreditsPurchaseError.InsufficientMana, null, BigInteger.MinusOne);

            // Every approval is a meta-transaction of the MANA token, so they share one nonce sequence.
            BigInteger minNonce = BigInteger.MinusOne;

            foreach (ManaSpend spend in spends)
            {
                ManaApprovalResult approval = await EnsureAllowanceAsync(buyer, spend.Spender, spend.Wei, minNonce, setState, signCt, settleCt);

                if (!approval.Success)
                    return approval;

                if (approval.Nonce.Sign >= 0)
                    minNonce = approval.Nonce + BigInteger.One;
            }

            return new ManaApprovalResult(CreditsPurchaseError.None, null, BigInteger.MinusOne);
        }

        /// <summary>
        ///     Does nothing when the spender's allowance already covers requiredWei; otherwise asks the buyer to
        ///     sign an unlimited approval and waits for it to be mined. An approval that was broadcast but not
        ///     seen confirmed fails as <see cref="CreditsPurchaseError.ManaApprovalFailed" />.
        /// </summary>
        private async UniTask<ManaApprovalResult> EnsureAllowanceAsync(string buyer, string spender, BigInteger requiredWei, BigInteger minNonce, Action<CreditsPurchaseState> setState,
            CancellationToken signCt, CancellationToken settleCt)
        {
            BigInteger allowance;

            try { allowance = await manaTokenReader.AllowanceAsync(buyer, spender, signCt); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA allowance of {spender} could not be read: {e.Message}");
                return new ManaApprovalResult(CreditsPurchaseError.ManaApprovalFailed, e.Message, BigInteger.MinusOne);
            }

            if (allowance >= requiredWei)
                return new ManaApprovalResult(CreditsPurchaseError.None, null, BigInteger.MinusOne);

            setState(CreditsPurchaseState.ApprovingMana);

            RelayResult relay = await metaTxRelayer.RelayAsync(manaTokenContract, buyer, CreditsTradeEncoder.BuildUnlimitedApproveCalldata(spender), minNonce, signCt, settleCt);

            if (relay.Outcome != RelayOutcome.Broadcast || string.IsNullOrEmpty(relay.TxHash))
            {
                CreditsPurchaseError error = relay.Outcome == RelayOutcome.Broadcast ? CreditsPurchaseError.RelayerUnavailable : ErrorOf(relay.Outcome, CreditsPurchaseError.ManaApprovalFailed);

                if (relay.Outcome != RelayOutcome.SignatureRejected)
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA approval for {spender} not sent ({relay.Outcome}): {relay.Message}");

                return new ManaApprovalResult(error, relay.Message, relay.Nonce);
            }

            setState(CreditsPurchaseState.ConfirmingManaApproval);

            SettlementOutcome settlement = await settlementPoller.WaitForSettlementAsync(relay.TxHash!, SETTLEMENT_TIMEOUT, settleCt);

            if (settlement == SettlementOutcome.Confirmed)
            {
                ReportHub.Log(ReportCategory.CREDITS_PURCHASE, $"MANA approval {relay.TxHash} for {spender} confirmed");
                return new ManaApprovalResult(CreditsPurchaseError.None, null, relay.Nonce);
            }

            ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA approval {relay.TxHash} for {spender} ended as {settlement}");
            return new ManaApprovalResult(CreditsPurchaseError.ManaApprovalFailed, $"Approval {settlement}", relay.Nonce);
        }

        /// <summary>
        ///     Signs, relays and settles the call. <paramref name="signCt" /> is rethrown when cancelled before the
        ///     signature; the relay and the settlement wait run on <paramref name="settleCt" />.
        /// </summary>
        public async UniTask<PurchaseOutcome> ExecuteAsync(ManaPurchaseRequest request, Action<CreditsPurchaseState> setState, CancellationToken signCt, CancellationToken settleCt)
        {
            setState(CreditsPurchaseState.Signing);

            RelayResult relay = await metaTxRelayer.RelayAsync(request.Target, request.Buyer, request.Calldata, request.MinNonce, signCt, settleCt);

            if (relay.Outcome == RelayOutcome.AmbiguousBroadcast)
            {
                setState(CreditsPurchaseState.Failed);
                return new PurchaseOutcome(CreditsPurchaseError.SettlementPending, null, relay.Message, true, false, false, relay.Nonce);
            }

            if (relay.Outcome != RelayOutcome.Broadcast || string.IsNullOrEmpty(relay.TxHash))
            {
                if (relay.Outcome != RelayOutcome.SignatureRejected)
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA purchase {request.Label} not sent ({relay.Outcome}): {relay.Message}");

                setState(CreditsPurchaseState.Failed);
                return PurchaseOutcome.Failure(ErrorOf(relay.Outcome, CreditsPurchaseError.RelayerUnavailable), relay.Message);
            }

            string txHash = relay.TxHash!;
            setState(CreditsPurchaseState.WaitingSettlement);

            SettlementOutcome settlement = await settlementPoller.WaitForSettlementAsync(txHash, SETTLEMENT_TIMEOUT, settleCt);

            switch (settlement)
            {
                case SettlementOutcome.Confirmed:
                    setState(CreditsPurchaseState.Success);
                    return new PurchaseOutcome(CreditsPurchaseError.None, txHash, null, true, true, false, relay.Nonce);
                case SettlementOutcome.Reverted:
                    setState(CreditsPurchaseState.Failed);
                    return new PurchaseOutcome(CreditsPurchaseError.TransactionReverted, txHash, null, true, false, true, relay.Nonce);
                default:
                    setState(CreditsPurchaseState.Failed);
                    return new PurchaseOutcome(CreditsPurchaseError.SettlementPending, txHash, null, true, false, false, relay.Nonce);
            }
        }

        private static CreditsPurchaseError ErrorOf(RelayOutcome outcome, CreditsPurchaseError ambiguous) =>
            outcome switch
            {
                RelayOutcome.SignatureRejected => CreditsPurchaseError.SignatureRejected,
                RelayOutcome.SigningFailed => CreditsPurchaseError.SigningFailed,
                RelayOutcome.SignatureTimedOut => CreditsPurchaseError.SignatureTimedOut,
                RelayOutcome.RelayerRejected => CreditsPurchaseError.RelayerUnavailable,
                _ => ambiguous,
            };
    }
}
