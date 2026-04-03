namespace DCL.VoiceChat.Proximity.UI
{
    public enum ProximityVoiceChatState
    {
        DISABLED,
        HEARING,
        SPEAKING,
        SUPPRESSED, // when you have another more priority voice chat - Proximity or Community
    }
}
