using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>One accept or buy call paid straight from the buyer's MANA, already encoded.</summary>
    public readonly struct ManaPurchaseRequest
    {
        public readonly string Buyer;
        public readonly string Label;

        /// <summary>The marketplace or the CollectionStore: the contract that is called and that pulls the MANA.</summary>
        public readonly MetaTxContract Target;
        public readonly string Calldata;
        public readonly BigInteger MinNonce;

        public ManaPurchaseRequest(string buyer, string label, MetaTxContract target, string calldata, BigInteger minNonce)
        {
            Buyer = buyer;
            Label = label;
            Target = target;
            Calldata = calldata;
            MinNonce = minNonce;
        }
    }
}
