using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    public readonly struct ManaApprovalResult
    {
        public readonly CreditsPurchaseError Error;
        public readonly string? Message;

        /// <summary>The MANA meta-transaction nonce the approval used; negative when none was sent.</summary>
        public readonly BigInteger Nonce;

        public bool Success => Error == CreditsPurchaseError.None;

        public ManaApprovalResult(CreditsPurchaseError error, string? message, BigInteger nonce)
        {
            Error = error;
            Message = message;
            Nonce = nonce;
        }
    }
}
