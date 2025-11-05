using UnityEngine;

namespace DCL.Backpack.Gifting.Models
{
    public readonly struct WearableViewModel : IGiftableItemViewModel
    {
        public IGiftable Giftable { get; }
        public string Urn => Giftable.Urn;
        public string DisplayName { get; }
        public string? CategoryId { get; }
        public string? RarityId { get; }
        public ThumbnailState ThumbnailState { get; }
        public Sprite? Thumbnail { get; }

        public WearableViewModel(WearableGiftable giftable)
        {
            Giftable = giftable;
            DisplayName = giftable.Name;
            CategoryId = giftable.Wearable.DTO.Metadata.AbstractData.category;
            RarityId = giftable.Wearable.DTO.Metadata.rarity;
            ThumbnailState = ThumbnailState.NotLoaded;
            Thumbnail = null;
        }

        private WearableViewModel(
            IGiftable giftable,
            string displayName,
            string? categoryId,
            string? rarityId,
            ThumbnailState state,
            Sprite? thumbnail)
        {
            Giftable = giftable;
            DisplayName = displayName;
            CategoryId = categoryId;
            RarityId = rarityId;
            ThumbnailState = state;
            Thumbnail = thumbnail;
        }

        public WearableViewModel WithState(ThumbnailState newState, Sprite? newSprite = null)
        {
            return new WearableViewModel(Giftable, DisplayName, CategoryId, RarityId, newState, newSprite ?? Thumbnail);
        }
    }
}
