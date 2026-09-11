using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Shop
{
    /// <summary>
    ///     The overview datasets (trending, new creations, outfits) behind a TTL cache with in-flight coalescing, so
    ///     reopening the shop within the TTL is instant and concurrent openers share one request. A purchase
    ///     invalidates everything: stock and prices moved.
    /// </summary>
    public class ShopCatalogService
    {
        public const int OVERVIEW_ROW_SIZE = 12;

        private static readonly TimeSpan DEFAULT_TTL = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FETCH_TIMEOUT = TimeSpan.FromSeconds(30);
        private static readonly Color DEFAULT_GRADIENT_FROM = new (0xC6 / 255f, 0x40 / 255f, 0xCD / 255f);
        private static readonly Color DEFAULT_GRADIENT_TO = new (0x69 / 255f, 0x1F / 255f, 0xA9 / 255f);

        private readonly MarketplaceShopAPIClient api;
        private readonly TimeSpan ttl;
        private readonly System.Random random = new ();
        private readonly Slot<IReadOnlyList<ShopItemCardModel>> trending = new ();
        private readonly Slot<IReadOnlyList<ShopItemCardModel>> newCreations = new ();
        private readonly Slot<ShopOutfitsDataset> outfits = new ();

        public ShopCatalogService(MarketplaceShopAPIClient api) : this(api, DEFAULT_TTL) { }

        public ShopCatalogService(MarketplaceShopAPIClient api, TimeSpan ttl)
        {
            this.api = api;
            this.ttl = ttl;
        }

        public UniTask<IReadOnlyList<ShopItemCardModel>> GetTrendingAsync(CancellationToken ct) =>
            GetOrFetchAsync(trending, FetchTrendingAsync, ct);

        public UniTask<IReadOnlyList<ShopItemCardModel>> GetNewCreationsAsync(CancellationToken ct) =>
            GetOrFetchAsync(newCreations, FetchNewCreationsAsync, ct);

        public UniTask<ShopOutfitsDataset> GetOutfitsAsync(CancellationToken ct) =>
            GetOrFetchAsync(outfits, FetchOutfitsAsync, ct);

        public void Invalidate()
        {
            trending.Clear();
            newCreations.Clear();
            outfits.Clear();
        }

        private async UniTask<T> GetOrFetchAsync<T>(Slot<T> slot, Func<CancellationToken, UniTask<T>> fetch, CancellationToken ct) where T: class
        {
            if (slot.Value != null && DateTime.UtcNow - slot.FetchedAtUtc < ttl)
                return slot.Value;

            UniTaskCompletionSource<T>? inFlight = slot.InFlight;

            if (inFlight == null)
            {
                inFlight = new UniTaskCompletionSource<T>();
                slot.InFlight = inFlight;
                FetchIntoAsync(slot, fetch, inFlight).Forget();
            }

            return await inFlight.Task.AttachExternalCancellation(ct);
        }

        private async UniTaskVoid FetchIntoAsync<T>(Slot<T> slot, Func<CancellationToken, UniTask<T>> fetch, UniTaskCompletionSource<T> completion) where T: class
        {
            using var timeoutCts = new CancellationTokenSource(FETCH_TIMEOUT);

            try
            {
                T value = await fetch(timeoutCts.Token);
                slot.Value = value;
                slot.FetchedAtUtc = DateTime.UtcNow;
                completion.TrySetResult(value);
            }
            catch (OperationCanceledException)
            {
                completion.TrySetException(new TimeoutException("The shop catalogue did not answer in time"));
            }
            catch (Exception e)
            {
                completion.TrySetException(e);
            }
            finally
            {
                slot.InFlight = null;
            }
        }

        private async UniTask<IReadOnlyList<ShopItemCardModel>> FetchTrendingAsync(CancellationToken ct)
        {
            ShopListingDto[] rows = await api.GetTrendingAsync(OVERVIEW_ROW_SIZE, ct);
            return ToModels(rows);
        }

        private async UniTask<IReadOnlyList<ShopItemCardModel>> FetchNewCreationsAsync(CancellationToken ct)
        {
            var query = new ShopCatalogQuery { First = OVERVIEW_ROW_SIZE, Sort = ShopSort.Newest };
            ShopListingsResponse response = await api.GetShopItemsAsync(query, ct);
            return ToModels(response.data);
        }

        private async UniTask<ShopOutfitsDataset> FetchOutfitsAsync(CancellationToken ct)
        {
            OutfitDto[] dtos = await api.GetOutfitsAsync(ct);

            if (dtos.Length == 0)
                return ShopOutfitsDataset.EMPTY;

            var keys = new SortedSet<string>(StringComparer.Ordinal);

            foreach (OutfitDto dto in dtos)
            {
                foreach (OutfitItemRefDto item in dto.items)
                    keys.Add(ItemKey(item.contractAddress, item.itemId));
            }

            var keyList = new List<string>(keys);
            var resolutionFailed = false;
            CatalogItemDto[] items;

            try { items = await api.GetCatalogItemsByIdsAsync(keyList, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.UI, $"Outfit items could not be resolved: {e.Message}");
                items = Array.Empty<CatalogItemDto>();
                resolutionFailed = true;
            }

            var modelsByKey = new Dictionary<string, ShopItemCardModel>(items.Length, StringComparer.OrdinalIgnoreCase);

            foreach (CatalogItemDto item in items)
                modelsByKey[ItemKey(item.contractAddress, item.itemId ?? string.Empty)] = ShopItemCardModel.FromCatalogItem(item);

            var models = new List<ShopOutfitModel>(dtos.Length);

            foreach (OutfitDto dto in dtos)
            {
                if (!dto.published || dto.items.Length == 0)
                    continue;

                var resolved = new List<ShopItemCardModel>(dto.items.Length);
                var missing = 0;
                var buyable = true;

                foreach (OutfitItemRefDto item in dto.items)
                {
                    if (modelsByKey.TryGetValue(ItemKey(item.contractAddress, item.itemId), out ShopItemCardModel? model))
                    {
                        resolved.Add(model);
                        buyable &= IsBuyableFromCreator(model);
                    }
                    else
                        missing++;
                }

                if (!resolutionFailed && (missing > 0 || !buyable))
                    continue;

                if (!ShopHexColor.TryParse(dto.gradientFrom, out Color from))
                    from = DEFAULT_GRADIENT_FROM;

                if (!ShopHexColor.TryParse(dto.gradientTo, out Color to))
                    to = DEFAULT_GRADIENT_TO;

                models.Add(new ShopOutfitModel(dto, resolved, missing, api.OutfitThumbnailUrl(dto.thumbnailHash), from, to));
            }

            Shuffle(models);
            return new ShopOutfitsDataset(models, resolutionFailed);
        }

        private static bool IsBuyableFromCreator(ShopItemCardModel model) =>
            model.PriceCredits > 0 && model.HasCreatorMint && model.Available is > 0;

        private static string ItemKey(string contractAddress, string itemId) =>
            $"{contractAddress.ToLowerInvariant()}-{itemId}";

        private static IReadOnlyList<ShopItemCardModel> ToModels(ShopListingDto[]? rows)
        {
            if (rows == null || rows.Length == 0)
                return Array.Empty<ShopItemCardModel>();

            var models = new ShopItemCardModel[rows.Length];

            for (var i = 0; i < rows.Length; i++)
                models[i] = ShopItemCardModel.FromListing(rows[i]);

            return models;
        }

        private void Shuffle(List<ShopOutfitModel> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private sealed class Slot<T> where T: class
        {
            public T? Value;
            public DateTime FetchedAtUtc;
            public UniTaskCompletionSource<T>? InFlight;

            public void Clear()
            {
                Value = null;
                FetchedAtUtc = DateTime.MinValue;
            }
        }
    }
}
