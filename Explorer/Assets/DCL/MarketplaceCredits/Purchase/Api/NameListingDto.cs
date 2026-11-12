using System;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits.Purchase
{
    // Server schema: marketplace-server GET /v1/nfts response envelope (src/ports/nfts/types.ts GetNFTsResponse).
    [Serializable]
    public class NameListingsResponse
    {
        public NameListingDto[]? data;
        public int total;
    }

    // Server schema: marketplace-server src/ports/nfts/types.ts NFTResult. The order is null for a name that is not listed.
    [Serializable]
    public class NameListingDto
    {
        public NameNftDto nft = null!;
        public NameOrderDto? order;
    }

    // Server schema: @dcl/schemas NFT (https://github.com/decentraland/schemas/blob/main/src/dapps/nft.ts), category "ens".
    [Serializable]
    public class NameNftDto
    {
        public string id = null!;
        public string name = null!;
        public string url = null!;
    }

    // Server schema: @dcl/schemas Order (https://github.com/decentraland/schemas/blob/main/src/dapps/order.ts). The price is in wei.
    [Serializable]
    public class NameOrderDto
    {
        public string price = null!;
    }
}
