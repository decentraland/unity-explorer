using CommunicationData.URLHelpers;
using DCL.ECSComponents;

namespace DCL.AvatarRendering.Emotes
{
    public struct EmotePendingToBroadcast
    {
        public URN EmoteId;
        public uint DurationMs;
        public AvatarEmoteMask Mask;
    }
}
