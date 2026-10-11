using DCL.ECSComponents;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerVoiceStateComponent : IDirtyMarker
    {
        public bool IsSpeaking;

        public bool IsDirty { get; set; }
    }
}
