using System.Runtime.CompilerServices;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    internal static class VoiceChatCallTypeValidator
    {
        public static bool IsNoActiveCall(VoiceChatType currentType, [CallerMemberName] string? callerName = null)
        {
            if (currentType != VoiceChatType.NONE)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Cannot {callerName} when already in a call");
                return false;
            }
            return true;
        }

        public static bool IsPrivateCall(VoiceChatType currentType, [CallerMemberName] string? callerName = null)
        {
            if (currentType != VoiceChatType.PRIVATE)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Cannot {callerName} when not in PRIVATE call");
                return false;
            }
            return true;
        }

        public static bool IsCommunityCall(VoiceChatType currentType, [CallerMemberName] string? callerName = null)
        {
            if (currentType != VoiceChatType.COMMUNITY)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Cannot {callerName} when not in COMMUNITY call");
                return false;
            }
            return true;
        }
    }
}
