using UnityEngine;

namespace DCL.Backpack.Gifting.Models
{
    public enum ThumbnailState { NotLoaded, Loading, Loaded, Error }
    
    public interface IGiftableItemViewModel
    {
        public GiftableAvatarAttachment Giftable { get; } 

        /// <summary>
        ///     Is item giftable?
        /// </summary>
        public bool IsGiftable { get; }

        /// <summary>Convenience pass-through of Giftable.Urn (used by selection/ids).</summary>
        string Urn { get; }

        /// <summary>Human-readable name for the footer/tooltip.</summary>
        string DisplayName { get; }

        /// <summary>Optional category identifier used by styling (e.g., "upper_body", "EMOTE").</summary>
        string? CategoryId { get; }

        /// <summary>Optional rarity identifier used by styling (e.g., "common", "epic").</summary>
        string? RarityId { get; }

        /// <summary>Thumbnail load state for the cell.</summary>
        ThumbnailState ThumbnailState { get; }

        /// <summary>Thumbnail sprite when loaded (null until Loaded).</summary>
        Sprite? Thumbnail { get; }

        bool IsEquipped { get; }

        int NftCount { get; }
    }
}