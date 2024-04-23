using CommunicationData.URLHelpers;
using DCL.ECSComponents;

namespace DCL.Multiplayer.SDK.Components
{
    public class PlayerEmoteSDKDataComponent : IDirtyMarker
    {
        public URN PreviousEmote;
        public URN PlayingEmote;
        public bool LoopingEmote;

        public bool IsDirty { get; set; }
    }
}
