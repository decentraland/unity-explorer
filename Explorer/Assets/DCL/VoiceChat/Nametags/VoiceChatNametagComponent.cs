namespace DCL.VoiceChat
{
    public struct VoiceChatNametagComponent
    {
        public readonly VoiceChatType Type; // Voice chat type that currently owns this indicator.

        public readonly bool IsSpeaking;
        public readonly bool IsHushed;

        public bool IsDirty;
        public bool IsRemoving;

        public VoiceChatNametagComponent(bool isSpeaking, VoiceChatType type, bool isHushed = false)
        {
            Type = type;

            IsSpeaking = isSpeaking;
            IsHushed = isHushed;

            IsDirty = true;
            IsRemoving = false;
        }
    }
}
