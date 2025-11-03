using DCL.AvatarRendering.Wearables.Components;
using UnityEngine;

namespace DCL.Backpack.Gifting.Models
{
    public enum ThumbnailState { NotLoaded, Loading, Loaded, Error }

    public interface IGiftableItemViewModel
    {
        public IWearable source { get; set; }
        string Urn { get; }
        ThumbnailState ThumbnailState { get; set; }
        Sprite? Thumbnail { get; }
    }
}