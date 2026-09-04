using DCL.MarketplaceCredits.Purchase;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Shop
{
    public sealed class ShopOutfitModel
    {
        public readonly OutfitDto Outfit;
        public readonly IReadOnlyList<ShopItemCardModel> ResolvedItems;

        public readonly int MissingCount;
        public readonly int TotalCredits;
        public readonly string? ThumbnailUrl;
        public readonly Color GradientFrom;
        public readonly Color GradientTo;

        public string Id => Outfit.id;

        public string Name => Outfit.name;

        public ShopOutfitModel(OutfitDto outfit, IReadOnlyList<ShopItemCardModel> resolvedItems, int missingCount, string? thumbnailUrl, Color gradientFrom, Color gradientTo)
        {
            Outfit = outfit;
            ResolvedItems = resolvedItems;
            MissingCount = missingCount;
            ThumbnailUrl = thumbnailUrl;
            GradientFrom = gradientFrom;
            GradientTo = gradientTo;

            var total = 0;

            foreach (ShopItemCardModel item in resolvedItems)
                total += item.PriceCredits;

            TotalCredits = total;
        }
    }

    public sealed class ShopOutfitsDataset
    {
        public static readonly ShopOutfitsDataset EMPTY = new (System.Array.Empty<ShopOutfitModel>(), false);
        public readonly IReadOnlyList<ShopOutfitModel> Outfits;
        public readonly bool ResolutionFailed;

        public ShopOutfitsDataset(IReadOnlyList<ShopOutfitModel> outfits, bool resolutionFailed)
        {
            Outfits = outfits;
            ResolutionFailed = resolutionFailed;
        }
    }

    public readonly struct ShopOutfitAddResult
    {
        public readonly string OutfitId;
        public readonly int Added;
        public readonly int SkippedUnavailable;
        public readonly int SkippedInCart;
        public readonly int SkippedOwn;
        public readonly int TotalCredits;

        public ShopOutfitAddResult(string outfitId, int added, int skippedUnavailable, int skippedInCart, int skippedOwn, int totalCredits)
        {
            OutfitId = outfitId;
            Added = added;
            SkippedUnavailable = skippedUnavailable;
            SkippedInCart = skippedInCart;
            SkippedOwn = skippedOwn;
            TotalCredits = totalCredits;
        }
    }

    public static class ShopHexColor
    {
        public static bool TryParse(string? hex, out Color color)
        {
            color = default(Color);

            if (string.IsNullOrEmpty(hex))
                return false;

            string digits = hex[0] == '#' ? hex.Substring(1) : hex;

            if (digits.Length != 6 && digits.Length != 8)
                return false;

            if (!TryParseByte(digits, 0, out byte r) || !TryParseByte(digits, 2, out byte g) || !TryParseByte(digits, 4, out byte b))
                return false;

            byte a = 255;

            if (digits.Length == 8 && !TryParseByte(digits, 6, out a))
                return false;

            color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            return true;
        }

        private static bool TryParseByte(string digits, int index, out byte value)
        {
            value = 0;
            int high = HexValue(digits[index]);
            int low = HexValue(digits[index + 1]);

            if (high < 0 || low < 0)
                return false;

            value = (byte)((high << 4) | low);
            return true;
        }

        private static int HexValue(char c) =>
            c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => -1,
            };
    }
}
