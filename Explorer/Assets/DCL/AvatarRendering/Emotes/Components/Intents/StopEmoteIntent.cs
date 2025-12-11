using CommunicationData.URLHelpers;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    /// Add this component to make the emote animation stop.
    /// </summary>
    public struct StopEmoteIntent
    {
        public URN EmoteUrn;

        public StopEmoteIntent(URN emoteUrn)
        {
            this.EmoteUrn = emoteUrn;
        }
    }
}
