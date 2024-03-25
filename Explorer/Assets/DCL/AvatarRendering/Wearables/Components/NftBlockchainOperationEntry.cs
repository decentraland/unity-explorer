using CommunicationData.URLHelpers;
using System;

namespace DCL.AvatarRendering.Wearables.Components
{
    public class NftBlockchainOperationEntry
    {
        public URN Urn { get; }
        public string TokenId { get; }
        public DateTime TransferredAt { get; }
        public decimal Price { get; }

        public NftBlockchainOperationEntry(URN urn, string tokenId, DateTime transferredAt, decimal price)
        {
            Urn = urn;
            TokenId = tokenId;
            TransferredAt = transferredAt;
            Price = price;
        }
    }
}
