using CommunicationData.URLHelpers;
using DCL.Diagnostics;

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
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "StopEmoteIntent " + emoteUrn);
            this.EmoteUrn = emoteUrn;
        }
    }
}
