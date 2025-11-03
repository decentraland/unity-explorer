using DCL.AvatarRendering.Wearables.Components;
using UnityEngine;

namespace DCL.Backpack.Gifting.Models
{
    public struct EmoteViewModel : IGiftableItemViewModel
    {
        public IWearable source { get; set; }
        public string Urn { get; }
        public ThumbnailState ThumbnailState { get; set; }
        public Sprite? Thumbnail { get; }
    }
}