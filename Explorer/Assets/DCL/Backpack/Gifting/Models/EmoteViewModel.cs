using DCL.Backpack.Gifting.Models;
using UnityEngine;

public readonly struct EmoteViewModel : IGiftableItemViewModel
{
    public IGiftable Giftable { get; }
    public string Urn => Giftable.Urn;
    public string DisplayName { get; }
    public string? CategoryId { get; }
    public string? RarityId { get; }
    public ThumbnailState ThumbnailState { get; }
    public Sprite? Thumbnail { get; }
    public bool IsEquipped { get; }
    public int NftCount { get; }

    public EmoteViewModel(EmoteGiftable giftable, int amount, bool isEquipped = false)
    {
        Giftable = giftable;
        DisplayName = giftable.Name;
        CategoryId = "emote";
        RarityId = giftable.Emote.DTO.Metadata.rarity;
        ThumbnailState = ThumbnailState.NotLoaded;
        Thumbnail = null;
        NftCount = amount;
        IsEquipped = isEquipped;
    }

    private EmoteViewModel(
        IGiftable giftable,
        string displayName,
        string? categoryId,
        string? rarityId,
        ThumbnailState state,
        Sprite? thumbnail,
        int amount,
        bool isEquipped)
    {
        Giftable = giftable;
        DisplayName = displayName;
        CategoryId = categoryId;
        RarityId = rarityId;
        ThumbnailState = state;
        Thumbnail = thumbnail;
        NftCount = amount;
        IsEquipped = isEquipped;
    }

    public EmoteViewModel WithState(ThumbnailState newState, Sprite? newSprite = null)
    {
        return new EmoteViewModel(Giftable,
            DisplayName,
            "emote",
            RarityId, newState,
            newSprite ?? Thumbnail,
            NftCount,
            IsEquipped);
    }
}