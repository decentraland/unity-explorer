namespace DCL.VoiceChat
{
    /// <summary>
    /// Marker component for muted (hushed) nearby participants.
    /// Independent of <see cref="VoiceChatNametagComponent"/> — added/removed by mute toggle,
    /// read by <see cref="DCL.Nametags.NametagPlacementSystem"/> to apply a visual indicator.
    /// </summary>
    public struct VoiceChatHushedComponent
    {
        public bool IsDirty;
        public readonly bool IsRemoving;

        public VoiceChatHushedComponent(bool isRemoving = false)
        {
            IsDirty = true;
            IsRemoving = isRemoving;
        }
    }
}
