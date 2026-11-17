using System;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits.Purchase
{
    // Server schema: shop-server GET /v1/outfits (Server/shop/app/src/lib/outfits.ts Outfit). Items are
    // contract/itemId pairs; the outfit carries no wearable URNs and no avatar colours.
    [Serializable]
    public class OutfitDto
    {
        public string id = null!;
        public string name = null!;
        public string thumbnailHash = null!;
        public OutfitItemRefDto[] items = null!;
        public string bodyShape = null!;
        public string gradientFrom = null!;
        public string gradientTo = null!;
        public string authorAddress = null!;
        public bool published;
        public long createdAt;
        public long updatedAt;
    }

    // Server schema: shop-server OutfitItemRef.
    [Serializable]
    public class OutfitItemRefDto
    {
        public string contractAddress = null!;
        public string itemId = null!;
    }

    // Server schema: shop-server GET /v1/outfits response envelope.
    [Serializable]
    public class OutfitsResponse
    {
        public OutfitDto[]? outfits;
    }
}
