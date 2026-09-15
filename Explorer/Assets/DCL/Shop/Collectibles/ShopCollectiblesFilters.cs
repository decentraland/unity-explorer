using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.Shop
{
    public enum ShopStatusFilter
    {
        OnSale,
        All,
        NotForSale,
    }

    public enum ShopSortOption
    {
        Newest,
        Cheapest,
        MostExpensive,
        Name,
    }

    public class ShopCollectiblesFilters
    {
        private static readonly Comparison<string> CANONICAL_RARITY_ORDER = static (a, b) => CanonicalRarityIndex(a).CompareTo(CanonicalRarityIndex(b));

        public string Category = ShopCategoryTree.ALL;
        public string? SubCategoryKey;
        public readonly List<string> Rarities = new ();
        public int? MinPriceCredits;
        public int? MaxPriceCredits;
        public ShopStatusFilter? ExplicitStatus;
        public bool Smart;
        public ShopSortOption Sort = ShopSortOption.Newest;
        public string SearchText = string.Empty;

        public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

        public ShopStatusFilter DefaultStatus => IsSearching ? ShopStatusFilter.All : ShopStatusFilter.OnSale;

        public ShopStatusFilter EffectiveStatus => ExplicitStatus ?? DefaultStatus;

        public bool UsesUnifiedFeed => EffectiveStatus == ShopStatusFilter.OnSale;

        public bool IsStatusChipVisible => ExplicitStatus != null && ExplicitStatus != DefaultStatus;

        public bool HasPriceRange => MinPriceCredits.HasValue || MaxPriceCredits.HasValue;

        public void Reset()
        {
            Category = ShopCategoryTree.ALL;
            SubCategoryKey = null;
            Rarities.Clear();
            MinPriceCredits = null;
            MaxPriceCredits = null;
            ExplicitStatus = null;
            Smart = false;
            Sort = ShopSortOption.Newest;
            SearchText = string.Empty;
        }

        public void ClearFilters()
        {
            string search = SearchText;
            Reset();
            SearchText = search;
        }

        public void ToggleRarity(string rarity)
        {
            if (Rarities.Remove(rarity))
                return;

            Rarities.Add(rarity);
            Rarities.Sort(CANONICAL_RARITY_ORDER);
        }

        private static int CanonicalRarityIndex(string rarity)
        {
            for (var i = 0; i < ShopCategoryTree.RARITIES.Count; i++)
            {
                if (ShopCategoryTree.RARITIES[i] == rarity)
                    return i;
            }

            return int.MaxValue;
        }

        public string BuildAnalyticsSignature()
        {
            var sb = new StringBuilder(96);
            sb.Append(Category).Append('|').Append(SubCategoryKey).Append('|');

            foreach (string rarity in Rarities)
                sb.Append(rarity).Append(',');

            sb.Append('|').Append(MinPriceCredits).Append('|').Append(MaxPriceCredits).Append('|')
              .Append(EffectiveStatus).Append('|').Append(Smart).Append('|').Append(Sort);

            return sb.ToString();
        }
    }
}
