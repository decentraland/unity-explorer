using CommunicationData.URLHelpers;
using DCL.ECSComponents;

namespace DCL.Multiplayer.SDK.Components
{
    public struct AvatarEmoteCommandComponent : IDirtyMarker
    {
        public URN PlayingEmote;
        public bool LoopingEmote;

        public bool IsDirty { get; set; }
    }
}
