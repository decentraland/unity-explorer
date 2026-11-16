namespace DCL.Shop
{
    /// <summary>
    ///     A shop section made of one card grid: the catalog sections (wearables, emotes) and the favorites.
    /// </summary>
    public interface IShopGridSection
    {
        void Activate();

        void Deactivate();

        void Reload();

        void SetCompact(bool compact);

        void SetSelected(string? key);

        void RefreshCartState();

        void RefreshCountdowns(long nowUnixSeconds);
    }
}
