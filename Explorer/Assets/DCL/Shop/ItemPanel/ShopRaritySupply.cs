namespace DCL.Shop
{
    /// <summary>
    ///     Max supply per rarity, the same numbers the marketplace prints next to the rarity ("EPIC 35/1000").
    /// </summary>
    public static class ShopRaritySupply
    {
        public static int? MaxSupply(string? rarity) =>
            rarity?.ToLowerInvariant() switch
            {
                "unique" => 1,
                "mythic" => 10,
                "exotic" => 50,
                "legendary" => 100,
                "epic" => 1000,
                "rare" => 5000,
                "uncommon" => 10000,
                "common" => 100000,
                _ => null,
            };

        public static string ChipLabel(string? rarity, string? issuedId)
        {
            string name = string.IsNullOrEmpty(rarity) ? string.Empty : rarity.ToUpperInvariant();
            int? max = MaxSupply(rarity);

            return max.HasValue && int.TryParse(issuedId, out int issued) && issued > 0
                ? $"{name} {issued}/{max.Value}"
                : name;
        }
    }
}
