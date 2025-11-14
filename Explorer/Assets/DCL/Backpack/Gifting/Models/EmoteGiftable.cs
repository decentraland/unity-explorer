using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;

namespace DCL.Backpack.Gifting.Models
{
    public readonly struct EmoteGiftable : IGiftable
    {
        public IEmote Emote { get; }
        public GiftableKind Kind => GiftableKind.Emote;
        public string Urn => Emote.GetUrn();
        public string Name => Emote.GetName();

        public string? CategoryOrNull => Emote.DTO.Metadata.AbstractData.category;
        public string? RarityOrNull => Emote.DTO.Metadata.rarity;
        public object Raw => Emote;

        public EmoteGiftable(IEmote e)
        {
            Emote = e;
        }
    }
}