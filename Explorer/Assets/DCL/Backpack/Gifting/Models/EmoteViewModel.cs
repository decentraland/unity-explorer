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

    public EmoteViewModel(EmoteGiftable giftable)
    {
        Giftable = giftable;
        DisplayName = giftable.Name;
        CategoryId = giftable.Emote.DTO.Metadata.AbstractData.category;
        RarityId = giftable.Emote.DTO.Metadata.rarity;
        ThumbnailState = ThumbnailState.NotLoaded;
        Thumbnail = null;
    }

    private EmoteViewModel(
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

    public EmoteViewModel WithState(ThumbnailState newState, Sprite? newSprite = null)
    {
        return new EmoteViewModel(Giftable, DisplayName, CategoryId, RarityId, newState, newSprite ?? Thumbnail);
    }
}