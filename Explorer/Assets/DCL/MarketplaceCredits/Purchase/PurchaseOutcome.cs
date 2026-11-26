using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    public readonly struct PurchaseOutcome
    {
        public readonly CreditsPurchaseError Error;
        public readonly string? TxHash;
        public readonly string? Message;
        public readonly bool Broadcast;
        public readonly bool Settled;
        public readonly bool Reverted;
        public readonly BigInteger Nonce;

        public bool Success => Error == CreditsPurchaseError.None;

        public PurchaseOutcome(CreditsPurchaseError error, string? txHash, string? message, bool broadcast, bool settled, bool reverted, BigInteger nonce)
        {
            Error = error;
            TxHash = txHash;
            Message = message;
            Broadcast = broadcast;
            Settled = settled;
            Reverted = reverted;
            Nonce = nonce;
        }

        public static PurchaseOutcome Failure(CreditsPurchaseError error, string? message = null) =>
            new (error, null, message, false, false, false, BigInteger.MinusOne);
    }
}
