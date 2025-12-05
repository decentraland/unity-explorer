using UnityEngine;

namespace DCL.Backpack.Gifting.Models
{
    public readonly struct GiftItemViewModel : IGiftableItemViewModel
    {
        public GiftableAvatarAttachment Giftable { get; }
        public string Urn => Giftable.Urn;
        public string DisplayName { get; }
        public string? CategoryId { get; }
        public string? RarityId { get; }
        public ThumbnailState ThumbnailState { get; }
        public Sprite? Thumbnail { get; }
        public bool IsEquipped { get; }
        public int NftCount { get; }
        public bool IsGiftable { get; }

        public GiftItemViewModel(GiftableAvatarAttachment giftable, int amount, bool isEquipped, bool isGiftable)
        {
            Giftable = giftable;
            DisplayName = giftable.Name;
            CategoryId = giftable.Category;
            RarityId = giftable.Rarity;
            ThumbnailState = ThumbnailState.NotLoaded;
            Thumbnail = null;
            NftCount = amount;
            IsEquipped = isEquipped;
            IsGiftable = isGiftable;
        }

        private GiftItemViewModel(
            GiftableAvatarAttachment giftable,
            string displayName,
            string? categoryId,
            string? rarityId,
            ThumbnailState state,
            Sprite? thumbnail,
            int amount,
            bool isEquipped,
            bool isGiftable)
        {
            Giftable = giftable;
            DisplayName = displayName;
            CategoryId = categoryId;
            RarityId = rarityId;
            ThumbnailState = state;
            Thumbnail = thumbnail;
            NftCount = amount;
            IsEquipped = isEquipped;
            IsGiftable = isGiftable;
        }

        public GiftItemViewModel WithState(ThumbnailState newState, Sprite? newSprite = null)
        {
            return new GiftItemViewModel(Giftable,
                DisplayName,
                CategoryId,
                RarityId, newState,
                newSprite ?? Thumbnail,
                NftCount,
                IsEquipped,
                IsGiftable);
        }
    }
}