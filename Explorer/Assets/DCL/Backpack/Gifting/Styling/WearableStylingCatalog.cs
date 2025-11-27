using DCL.Backpack.Gifting.Views;
using UnityEngine;

namespace DCL.Backpack.Gifting.Styling
{
    public sealed class WearableStylingCatalog : IWearableStylingCatalog
    {
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NftTypeIconSO categoryIcons;

        public WearableStylingCatalog(NFTColorsSO rarityColors,
            NftTypeIconSO rarityBackgrounds,
            NftTypeIconSO categoryIcons)
        {
            this.rarityColors = rarityColors;
            this.rarityBackgrounds = rarityBackgrounds;
            this.categoryIcons = categoryIcons;
        }

        public Sprite GetRarityBackground(string? rarity)
        {
            return rarityBackgrounds.GetTypeImage(rarity);
        }

        public Color GetRarityFlapColor(string? rarity)
        {
            return rarityColors.GetColor(rarity);
        }

        public Sprite GetCategoryIcon(string? category)
        {
            return categoryIcons.GetTypeImage(category);
        }

        public GiftItemStyleSnapshot GetStyleSnapshot(string? rarity, string? category)
        {
            var rarityBg = GetRarityBackground(rarity ?? "base");
            var flapCol = GetRarityFlapColor(rarity ?? "base");
            var catIcon = GetCategoryIcon(category);

            return new GiftItemStyleSnapshot(catIcon, rarityBg, flapCol);
        }
    }
}