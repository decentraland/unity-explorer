using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     marketplace-server / shop-server reads used by the shop and the credits purchase flow. All members are
    ///     virtual so the class can be substituted in tests; the URL assembly is exposed as internal statics so the
    ///     wire contract can be asserted without a web request.
    /// </summary>
    public class MarketplaceShopAPIClient
    {
        /// <summary>
        ///     The default favorites list of marketplace-server: one well-known row shared by every account and every
        ///     environment, with the picks scoped per signer.
        /// </summary>
        public const string DEFAULT_FAVORITES_LIST_ID = "70ab6873-4a03-4eb2-b331-4b8be0e0b8af";

        private const int FAVORITES_PAGE_SIZE = 100;
        private const int CATALOG_IDS_CHUNK_SIZE = 50;
        private const int OUTFIT_THUMBNAIL_HASH_LENGTH = 64;
        private const string UNIFIED_PATH = "v3/catalog/unified";
        private const string TRENDING_PATH = "v3/catalog/trending";
        private const string CATALOG_ITEMS_PATH = "v3/catalog/items";
        private const string OUTFITS_PATH = "v1/outfits";
        private const string OUTFIT_THUMBNAILS_PATH = "v1/outfits/thumbnails";

        private static readonly URLParameter LISTING_TYPE_PRIMARY = new ("listingType", "primary");
        private static readonly URLParameter GROUP_BY_ITEM = new ("groupBy", "item");
        private static readonly URLParameter EXCLUDE_SOCIAL_EMOTES = new ("includeSocialEmotes", "false");
        private static readonly URLParameter IS_SMART = new ("isSmart", "true");
        private static readonly URLParameter IS_WEARABLE_SMART = new ("isWearableSmart", "true");

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string marketplaceServerBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.MarketplaceServer);
        private string shopServerBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.ShopServer);

        public MarketplaceShopAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public virtual UniTask<ShopListingDto?> GetShopListingForItemAsync(string contractAddress, string itemId, CancellationToken ct) =>
            GetShopListingForItemAsync(contractAddress, itemId, primaryOnly: true, ct);

        public virtual async UniTask<ShopListingDto?> GetShopListingForItemAsync(string contractAddress, string itemId, bool primaryOnly, CancellationToken ct)
        {
            URLAddress url;

            using (decentralandUrlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder))
            {
                urlBuilder.AppendPath(new URLPath(UNIFIED_PATH));
                urlBuilder.AppendParameter(new URLParameter("contractAddress", contractAddress));
                urlBuilder.AppendParameter(new URLParameter("itemId", itemId));
                urlBuilder.AppendParameter(new URLParameter("first", "1"));
                urlBuilder.AppendParameter(primaryOnly ? LISTING_TYPE_PRIMARY : GROUP_BY_ITEM);
                url = urlBuilder.Build();
            }

            ShopListingsResponse response = await GetListingsAsync(url, ct);
            return response.data is { Length: > 0 } ? response.data[0] : null;
        }

        public virtual async UniTask<TradeDto?> GetTradeAsync(string tradeId, CancellationToken ct)
        {
            var url = $"{marketplaceServerBaseUrl}/v1/trades/{tradeId}";

            TradeResponse response = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.CREDITS_PURCHASE)
                                                               .CreateFromJson<TradeResponse>(WRJsonParser.Newtonsoft);

            return response.data;
        }

        public virtual async UniTask<ShopListingDto[]> GetTrendingAsync(int first, CancellationToken ct)
        {
            ShopListingsResponse response = await GetListingsAsync(BuildTrendingUrl(decentralandUrlsSource, first), ct);
            return response.data ?? Array.Empty<ShopListingDto>();
        }

        public virtual UniTask<ShopListingsResponse> GetShopItemsAsync(ShopCatalogQuery query, CancellationToken ct) =>
            GetListingsAsync(BuildShopItemsUrl(decentralandUrlsSource, query), ct);

        public virtual UniTask<CatalogItemsResponse> GetCatalogItemsAsync(ShopCatalogQuery query, CancellationToken ct) =>
            GetCatalogItemsAsync(BuildCatalogItemsUrl(decentralandUrlsSource, query), ct);

        public virtual async UniTask<CatalogItemDto[]> GetCatalogItemsByIdsAsync(IReadOnlyList<string> ids, CancellationToken ct)
        {
            if (ids.Count == 0)
                return Array.Empty<CatalogItemDto>();

            var byId = new Dictionary<string, CatalogItemDto>(ids.Count, StringComparer.OrdinalIgnoreCase);

            for (var start = 0; start < ids.Count; start += CATALOG_IDS_CHUNK_SIZE)
            {
                int count = Math.Min(CATALOG_IDS_CHUNK_SIZE, ids.Count - start);
                CatalogItemsResponse response = await GetCatalogItemsAsync(BuildCatalogItemsByIdsUrl(decentralandUrlsSource, ids, start, count), ct);

                if (response.data == null)
                    continue;

                foreach (CatalogItemDto item in response.data)
                    byId[item.id] = item;
            }

            var ordered = new List<CatalogItemDto>(byId.Count);

            foreach (string id in ids)
            {
                if (byId.TryGetValue(id, out CatalogItemDto? item))
                    ordered.Add(item);
            }

            return ordered.ToArray();
        }

        /// <summary>
        ///     Every item id (contract-itemId) the signed-in account saved in the default favorites list, newest first.
        /// </summary>
        public virtual async UniTask<List<string>> GetFavoriteIdsAsync(CancellationToken ct)
        {
            var ids = new List<string>();
            var total = 0;

            do
            {
                FavoritePicksResponse response = await webRequestController.SignedFetchGetAsync(new CommonArguments(BuildFavoritePicksUrl(decentralandUrlsSource, ids.Count)), string.Empty, ct)
                                                                           .CreateFromJson<FavoritePicksResponse>(WRJsonParser.Newtonsoft);

                if (!response.ok || response.data == null)
                    throw new Exception($"Favorites could not be read: {response.message}");

                // An empty page ends the read even when the reported total was never reached
                if (response.data.results.Length == 0)
                    break;

                foreach (FavoritePickDto pick in response.data.results)
                    ids.Add(pick.itemId);

                total = response.data.total;
            }
            while (ids.Count < total);

            return ids;
        }

        public virtual async UniTask SetFavoriteAsync(string itemKey, bool favorite, CancellationToken ct)
        {
            FavoriteToggleResponse response = await webRequestController.SignedFetchPostAsync(
                                                                             new CommonArguments(BuildFavoriteToggleUrl(decentralandUrlsSource, itemKey)),
                                                                             GenericPostArguments.CreateJson(BuildFavoriteToggleBody(favorite)),
                                                                             string.Empty,
                                                                             ct)
                                                                        .CreateFromJson<FavoriteToggleResponse>(WRJsonParser.Newtonsoft);

            if (!response.ok)
                throw new Exception($"Favorite {itemKey} could not be updated: {response.message}");
        }

        public virtual async UniTask<OutfitDto[]> GetOutfitsAsync(CancellationToken ct)
        {
            var url = URLAddress.FromString($"{shopServerBaseUrl}/{OUTFITS_PATH}");

            OutfitsResponse response = await webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.CREDITS_PURCHASE)
                                                                 .CreateFromJson<OutfitsResponse>(WRJsonParser.Newtonsoft);

            return response.outfits ?? Array.Empty<OutfitDto>();
        }

        public virtual string? OutfitThumbnailUrl(string? thumbnailHash) =>
            IsOutfitThumbnailHash(thumbnailHash) ? $"{shopServerBaseUrl}/{OUTFIT_THUMBNAILS_PATH}/{thumbnailHash}" : null;

        internal static bool IsOutfitThumbnailHash(string? hash)
        {
            if (hash == null || hash.Length != OUTFIT_THUMBNAIL_HASH_LENGTH)
                return false;

            foreach (char c in hash)
            {
                bool isHex = c is >= '0' and <= '9' or >= 'a' and <= 'f';

                if (!isHex)
                    return false;
            }

            return true;
        }

        internal static URLAddress BuildTrendingUrl(IDecentralandUrlsSource urlsSource, int first)
        {
            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath(TRENDING_PATH));
            urlBuilder.AppendParameter(new URLParameter("first", first.ToString()));
            urlBuilder.AppendParameter(EXCLUDE_SOCIAL_EMOTES);
            urlBuilder.AppendParameter(LISTING_TYPE_PRIMARY);
            return urlBuilder.Build();
        }

        internal static URLAddress BuildShopItemsUrl(IDecentralandUrlsSource urlsSource, ShopCatalogQuery query)
        {
            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath(UNIFIED_PATH));
            AppendPaging(urlBuilder, query);
            AppendCategory(urlBuilder, query.Category);
            AppendCsv(urlBuilder, "wearableCategory", query.WearableCategories);
            AppendCsv(urlBuilder, "rarity", query.Rarities);

            if (query.MinPriceCredits.HasValue)
                urlBuilder.AppendParameter(new URLParameter("minPriceCredits", query.MinPriceCredits.Value.ToString()));

            if (query.MaxPriceCredits.HasValue)
                urlBuilder.AppendParameter(new URLParameter("maxPriceCredits", query.MaxPriceCredits.Value.ToString()));

            AppendSearch(urlBuilder, query.Search);
            urlBuilder.AppendParameter(new URLParameter("sortBy", ShopCatalogQueryWire.SortToWire(query.Sort)));

            if (query.SmartOnly)
                urlBuilder.AppendParameter(IS_SMART);

            urlBuilder.AppendParameter(LISTING_TYPE_PRIMARY);
            urlBuilder.AppendParameter(GROUP_BY_ITEM);
            return urlBuilder.Build();
        }

        internal static URLAddress BuildCatalogItemsUrl(IDecentralandUrlsSource urlsSource, ShopCatalogQuery query)
        {
            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath(CATALOG_ITEMS_PATH));
            AppendPaging(urlBuilder, query);
            AppendCategory(urlBuilder, query.Category);
            AppendRepeated(urlBuilder, "wearableCategory", query.WearableCategories);
            AppendRepeated(urlBuilder, "rarity", query.Rarities);
            AppendSearch(urlBuilder, query.Search);
            urlBuilder.AppendParameter(new URLParameter("sortBy", ShopCatalogQueryWire.SortToWire(query.Sort)));

            if (query.SmartOnly)
                urlBuilder.AppendParameter(IS_WEARABLE_SMART);

            if (query.IsOnSale.HasValue)
                urlBuilder.AppendParameter(new URLParameter("isOnSale", query.IsOnSale.Value ? "true" : "false"));

            urlBuilder.AppendParameter(EXCLUDE_SOCIAL_EMOTES);
            return urlBuilder.Build();
        }

        internal static URLAddress BuildCatalogItemsByIdsUrl(IDecentralandUrlsSource urlsSource, IReadOnlyList<string> ids, int start, int count)
        {
            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath(CATALOG_ITEMS_PATH));
            urlBuilder.AppendParameter(new URLParameter("first", count.ToString()));

            for (int i = start; i < start + count; i++)
                urlBuilder.AppendParameter(new URLParameter("id", ids[i]));

            return urlBuilder.Build();
        }

        internal static URLAddress BuildFavoritePicksUrl(IDecentralandUrlsSource urlsSource, int offset)
        {
            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath($"v1/lists/{DEFAULT_FAVORITES_LIST_ID}/picks"));
            urlBuilder.AppendParameter(new URLParameter("limit", FAVORITES_PAGE_SIZE.ToString()));
            urlBuilder.AppendParameter(new URLParameter("offset", offset.ToString()));
            return urlBuilder.Build();
        }

        internal static URLAddress BuildFavoriteToggleUrl(IDecentralandUrlsSource urlsSource, string itemKey)
        {
            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath($"v1/picks/{itemKey}"));
            return urlBuilder.Build();
        }

        internal static string BuildFavoriteToggleBody(bool favorite) =>
            favorite
                ? $"{{\"pickedFor\":[\"{DEFAULT_FAVORITES_LIST_ID}\"]}}"
                : $"{{\"unpickedFrom\":[\"{DEFAULT_FAVORITES_LIST_ID}\"]}}";

        private UniTask<ShopListingsResponse> GetListingsAsync(URLAddress url, CancellationToken ct) =>
            webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.CREDITS_PURCHASE)
                                .CreateFromJson<ShopListingsResponse>(WRJsonParser.Newtonsoft);

        private UniTask<CatalogItemsResponse> GetCatalogItemsAsync(URLAddress url, CancellationToken ct) =>
            webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.CREDITS_PURCHASE)
                                .CreateFromJson<CatalogItemsResponse>(WRJsonParser.Newtonsoft);

        private static void AppendPaging(URLBuilder urlBuilder, in ShopCatalogQuery query)
        {
            urlBuilder.AppendParameter(new URLParameter("first", query.First.ToString()));

            if (query.Skip > 0)
                urlBuilder.AppendParameter(new URLParameter("skip", query.Skip.ToString()));
        }

        private static void AppendCategory(URLBuilder urlBuilder, ShopItemCategory category)
        {
            string? wire = ShopCatalogQueryWire.CategoryToWire(category);

            if (wire != null)
                urlBuilder.AppendParameter(new URLParameter("category", wire));
        }

        private static void AppendSearch(URLBuilder urlBuilder, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return;

            urlBuilder.AppendParameter(new URLParameter("search", Uri.EscapeDataString(search.Trim())));
        }

        private static void AppendCsv(URLBuilder urlBuilder, string name, IReadOnlyList<string>? values)
        {
            if (values == null || values.Count == 0)
                return;

            urlBuilder.AppendParameter(new URLParameter(name, string.Join(",", values)));
        }

        private static void AppendRepeated(URLBuilder urlBuilder, string name, IReadOnlyList<string>? values)
        {
            if (values == null)
                return;

            foreach (string value in values)
                urlBuilder.AppendParameter(new URLParameter(name, value));
        }
    }
}
