using System;

namespace OutfitStudio
{
    /// <summary>
    /// Query parameters for the marketplace catalog endpoint
    /// (GET marketplace-api.decentraland.{org|zone}/v2/catalog).
    /// </summary>
    public class CatalogQuery
    {
        /// <summary>"wearable" or "emote".</summary>
        public string Category = "wearable";

        /// <summary>Free text search across names and descriptions.</summary>
        public string Search;

        /// <summary>Wearable slot filter (upper_body, lower_body, feet, hat, ...). Null = any.</summary>
        public string WearableCategory;

        /// <summary>Emote category filter (dance, poses, fun, ...). Null = any.</summary>
        public string EmoteCategory;

        /// <summary>Rarity filter (common ... unique). Null = any.</summary>
        public string Rarity;

        /// <summary>Gender filter (male, female, unisex). Null = any.</summary>
        public string Gender;

        /// <summary>Only items currently buyable (mintable from the collection store, or with at least
        /// one open listing). Sent to the API as <c>isOnSale=true</c> when set, and omitted entirely
        /// when not - <c>isOnSale=false</c> is not the neutral value, the endpoint reads it as "only
        /// items that are NOT on sale".</summary>
        public bool IsOnSale;

        /// <summary>Only primary sales: items still mintable from their creator's collection store,
        /// with every secondary (listing-only) sale dropped. Sent as <c>onlyMinting=true</c> and
        /// omitted when not set. Verified against the live API: it implies on-sale-ness on its own
        /// (same total with or without <c>isOnSale=true</c>), and every item it returns is mintable
        /// with zero open listings - so it's strictly narrower than <see cref="IsOnSale"/>, which is
        /// why the UI forces that toggle on alongside it.</summary>
        public bool OnlyMinting;

        /// <summary>
        /// Sort order. Real marketplace values: newest, recently_listed, recently_sold, cheapest,
        /// most_expensive. "name" is a local-only convenience option, not sent to the API.
        /// </summary>
        public string SortBy = "newest";

        /// <summary>Specific URNs to look up (used to hydrate slot names/thumbnails).</summary>
        public string[] Urns;

        /// <summary>Filter by published collection contract address (0x...).</summary>
        public string ContractAddress;

        public int First = 24;
        public int Skip;
    }

    [Serializable]
    public class CatalogPage
    {
        public CatalogItem[] data;
        public int total;
    }

    /// <summary>
    /// A marketplace item as returned by /v2/catalog. Only the fields we consume are declared;
    /// JsonUtility ignores the rest of the payload.
    /// </summary>
    [Serializable]
    public class CatalogItem
    {
        public string id;
        public string name;
        public string thumbnail;
        public string urn;
        public string category; // "wearable" | "emote"
        public string rarity;

        /// <summary>Primary sale only: true when the item can still be minted from the collection
        /// store. False for a sold-out (or never-listed) item even when it has open secondary
        /// listings - use <see cref="IsBuyable"/> for the marketplace's "on sale" notion.</summary>
        public bool isOnSale;

        /// <summary>Number of open secondary listings.</summary>
        public int listings;

        public string price; // primary-sale price in wei, as a decimal string ("0" when not mintable)

        /// <summary>Cheapest way to acquire the item in wei (primary price or lowest listing),
        /// as a decimal string. 2^256-1 when it isn't buyable at all.</summary>
        public string minPrice;

        public long createdAt; // unix seconds; 0 when absent
        public long updatedAt; // unix seconds; bumped when the item is (re)listed
        public long soldAt; // unix seconds; 0 if never sold
        public ItemData data;

        /// <summary>The avatar slot this item occupies (wearable category or "emote").</summary>
        public string Slot => category == "emote" ? "emote" : data?.wearable?.category;

        /// <summary>
        /// What the web marketplace calls "on sale": mintable from the store OR carrying at least one
        /// open listing. Matches the set the endpoint's own <c>isOnSale=true</c> filter returns, which
        /// is deliberately wider than the <see cref="isOnSale"/> field on its own.
        /// </summary>
        public bool IsBuyable => isOnSale || listings > 0;

        [Serializable]
        public class ItemData
        {
            public WearableData wearable;
            public EmoteData emote;
        }

        [Serializable]
        public class WearableData
        {
            public string[] bodyShapes;
            public string category;
            public bool isSmart;
        }

        [Serializable]
        public class EmoteData
        {
            public string[] bodyShapes;
            public string category;
            public bool loop;
        }
    }
}
