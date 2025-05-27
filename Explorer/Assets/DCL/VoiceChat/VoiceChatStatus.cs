namespace DCL.VoiceChat
{
    public enum VoiceChatStatus
    {
        //Default status when no voice chat is started
        DISCONNECTED,
        //Remote state when backend detects an incoming call
        VOICE_CHAT_RECEIVED_CALL,
        //Local state when user starts a call
        VOICE_CHAT_STARTING_CALL,
        //Remote state when backend confirms a voice chat started
        VOICE_CHAT_STARTED_CALL,
        //Remote state when backend confirms a voice chat is in progress
        VOICE_CHAT_IN_CALL,
        //Local state when user ends a call
        VOICE_CHAT_ENDING_CALL,
        //Remote state when backend confirms a voice chat has ended
        VOICE_CHAT_ENDED_CALL
    }
}
