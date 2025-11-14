using UnityEngine;

namespace DCL.Backpack.Gifting.Models
{
    public enum ThumbnailState { NotLoaded, Loading, Loaded, Error }

    /// <summary>
    ///     ViewModel the grid & cell need. It is UI-facing and agnostic to the
    ///     underlying SDK types (IWearable, IEmote). Everything specific lives
    ///     behind IGiftableAttachment.
    /// </summary>
    public interface IGiftableItemViewModel
    {
        /// <summary>Unified attachment wrapper (Wearable or Emote).</summary>
        IGiftable Giftable { get; }

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