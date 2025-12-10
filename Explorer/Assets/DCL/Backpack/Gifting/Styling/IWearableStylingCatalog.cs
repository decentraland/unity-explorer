using DCL.Backpack.Gifting.Views;
using UnityEngine;

namespace DCL.Backpack.Gifting.Styling
{
    public interface IWearableStylingCatalog
    {
        Sprite GetRarityBackground(string? rarity);
        Color GetRarityFlapColor(string? rarity);
        Sprite GetCategoryIcon(string? category);
        GiftItemStyleSnapshot GetStyleSnapshot(string? rarity, string? category);
    }
}