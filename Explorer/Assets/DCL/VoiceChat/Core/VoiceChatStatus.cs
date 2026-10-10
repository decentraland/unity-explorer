namespace DCL.VoiceChat
{
    public enum VoiceChatStatus
    {
        //Default status when no voice chat is started
        Disconnected,

        //Remote state when backend detects an incoming call
        VoiceChatReceivedCall,

        //Local state when user starts a call
        VoiceChatStartingCall,

        //Remote state when backend confirms a voice chat started
        VoiceChatStartedCall,

        //Remote state when backend confirms a voice chat is in progress
        VoiceChatInCall,

        //Local state when user ends a call
        VoiceChatEndingCall,

        //Local state when user rejects a call
        VoiceChatRejectingCall,

        //Remote status when user is busy
        VoiceChatBusy,

        //Generic error for unhandled exceptions
        VoiceChatGenericError,
    }

    public static class VoiceChatStatusExtensionMethods
    {
        public static bool IsNotConnected(this VoiceChatStatus status) =>
            status is
                VoiceChatStatus.Disconnected or
                VoiceChatStatus.VoiceChatBusy or
                VoiceChatStatus.VoiceChatGenericError;

        public static bool IsInCall(this VoiceChatStatus status) =>
            status is VoiceChatStatus.VoiceChatInCall;
    }
}
