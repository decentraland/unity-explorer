using System;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits.Purchase
{
    // Server schema: marketplace-server GET /v3/catalog/items (ports/catalog/utils.ts fromDBItemToCatalogItem),
    // mirrored by Server/shop/app/src/lib/collections.ts RawCatalogItem. The /v1/items row shape plus priceCredits.
    [Serializable]
    public class CatalogItemDto
    {
        public string id = null!;
        public string name = null!;
        public string contractAddress = null!;
        public string category = null!;
        public string? network;
        public string? creator;
        public string? thumbnail;
        public string? url;
        public string? urn;
        public string? rarity;
        public string? itemId;
        public string? tradeId;
        public string? price;
        public int chainId;
        public bool isOnSale;
        public CatalogItemDataDto? data;

        // Newtonsoft-deserialized wire DTO (CreateFromJson with WRJsonParser.Newtonsoft); Unity serialization never sees these fields.
        // `available` is serialized by the server as a number OR a string, which Newtonsoft coerces either way.
#pragma warning disable UAC1001
        public int? priceCredits;
        public int? available;
#pragma warning restore UAC1001
    }

    // Server schema: /v1/items `data` object (@dcl/schemas Wearable / Emote item data).
    [Serializable]
    public class CatalogItemDataDto
    {
        public CatalogWearableDataDto? wearable;
        public CatalogEmoteDataDto? emote;
    }

    [Serializable]
    public class CatalogWearableDataDto
    {
        public string? category;
        public string[]? bodyShapes;

#pragma warning disable UAC1001
        public bool? isSmart;
#pragma warning restore UAC1001
    }

    [Serializable]
    public class CatalogEmoteDataDto
    {
        public string? category;

#pragma warning disable UAC1001
        public bool? loop;
        public bool? hasSound;
#pragma warning restore UAC1001
    }

    // Server schema: marketplace-server GET /v3/catalog/items response envelope.
    [Serializable]
    public class CatalogItemsResponse
    {
        public CatalogItemDto[]? data;
        public int total;
    }

    public static class CatalogItemDtoExtensions
    {
        public const string GENDER_MALE = "male";
        public const string GENDER_FEMALE = "female";
        public const string GENDER_UNISEX = "unisex";

        private const string BASE_MALE_MARKER = "basemale";
        private const string BASE_FEMALE_MARKER = "basefemale";

        public static string? WearableCategory(this CatalogItemDto item) =>
            item.data?.wearable?.category ?? item.data?.emote?.category;

        public static bool IsSmart(this CatalogItemDto item) =>
            item.data?.wearable?.isSmart == true;

        public static string? Gender(this CatalogItemDto item)
        {
            string[]? bodyShapes = item.data?.wearable?.bodyShapes;

            if (bodyShapes == null || bodyShapes.Length == 0)
                return null;

            var male = false;
            var female = false;

            foreach (string shape in bodyShapes)
            {
                if (shape.IndexOf(BASE_FEMALE_MARKER, StringComparison.OrdinalIgnoreCase) >= 0)
                    female = true;
                else if (shape.IndexOf(BASE_MALE_MARKER, StringComparison.OrdinalIgnoreCase) >= 0)
                    male = true;
            }

            if (male && female)
                return GENDER_UNISEX;

            if (male)
                return GENDER_MALE;

            return female ? GENDER_FEMALE : null;
        }
    }
}
