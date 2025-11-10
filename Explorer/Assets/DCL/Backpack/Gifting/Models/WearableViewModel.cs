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
        public bool IsEquipped { get; }

        public WearableViewModel(WearableGiftable giftable, bool isEquipped = false)
        {
            Giftable = giftable;
            DisplayName = giftable.Name;
            CategoryId = giftable.Wearable.DTO.Metadata.AbstractData.category;
            RarityId = giftable.Wearable.DTO.Metadata.rarity;
            ThumbnailState = ThumbnailState.NotLoaded;
            Thumbnail = null;
            IsEquipped = isEquipped;
        }
        
        private WearableViewModel(
            IGiftable giftable,
            string displayName,
            string? categoryId,
            string? rarityId,
            ThumbnailState state,
            Sprite? thumbnail,
            bool isEquipped)
        {
            Giftable = giftable;
            DisplayName = displayName;
            CategoryId = categoryId;
            RarityId = rarityId;
            ThumbnailState = state;
            Thumbnail = thumbnail;
            IsEquipped = isEquipped;
        }

        public WearableViewModel WithState(ThumbnailState newState, Sprite? newSprite = null)
        {
            return new WearableViewModel(
                Giftable,
                DisplayName,
                CategoryId,
                RarityId,
                newState,
                newSprite ?? Thumbnail,
                IsEquipped);
        }
    }
}
