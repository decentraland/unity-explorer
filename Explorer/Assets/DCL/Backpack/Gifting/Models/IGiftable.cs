using DCL.AvatarRendering.Loading.Components;

namespace DCL.Backpack.Gifting.Models
{
    public enum GiftableKind { Wearable, Emote }

    public interface IGiftable
    {
        GiftableKind Kind { get; }
        string Urn { get; }
        string Name { get; }

        string? CategoryOrNull { get; }
        string? RarityOrNull { get; }

        object Raw { get; }

        IAvatarAttachment GetAttachment(); 
    }
}