using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.Backpack.Gifting.Models
{
    public readonly struct WearableGiftable : IGiftable
    {
        public IWearable Wearable { get; }
        public GiftableKind Kind => GiftableKind.Wearable;
        public string Urn => Wearable.GetUrn();
        public string Name => Wearable.GetName();
        public string? CategoryOrNull => Wearable.DTO.Metadata.AbstractData.category;
        public string? RarityOrNull => Wearable.DTO.Metadata.rarity;
        public object Raw => Wearable;

        public WearableGiftable(IWearable w)
        {
            Wearable = w;
        }
    }
}