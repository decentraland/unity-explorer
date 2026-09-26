using System.Runtime.CompilerServices;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    internal static class VoiceChatCallTypeValidator
    {
        private const string TAG = nameof(VoiceChatCallTypeValidator);

        public static bool IsNoActiveCall(VoiceChatType currentType, [CallerMemberName] string? callerName = null)
        {
            if (currentType != VoiceChatType.None)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Cannot {callerName} when already in a call");
                return false;
            }
            return true;
        }

        public static bool IsPrivateCall(VoiceChatType currentType, [CallerMemberName] string? callerName = null)
        {
            if (currentType != VoiceChatType.Private)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Cannot {callerName} when not in PRIVATE call");
                return false;
            }
            return true;
        }

        public static bool IsCommunityCall(VoiceChatType currentType, [CallerMemberName] string? callerName = null)
        {
            if (currentType != VoiceChatType.Community)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Cannot {callerName} when not in COMMUNITY call");
                return false;
            }
            return true;
        }
    }
}
