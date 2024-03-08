namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteCache
    {
        bool TryGetEmote(string urn, out IEmote emote);
    }
}
