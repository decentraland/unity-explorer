using CommunicationData.URLHelpers;
using DCL.Diagnostics;

namespace DCL.AvatarRendering.Emotes
{
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
