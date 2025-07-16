using DCL.Diagnostics;
using DCL.Web3;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Implementation of voice chat call status service for community calls.
    /// Currently provides empty implementations as community voice chat is not yet implemented.
    /// </summary>
    public class CommunityVoiceChatCallStatusService : VoiceChatCallStatusServiceBase, ICommunityVoiceChatCallStatusService
    {
        public CommunityVoiceChatCallStatusService()
        {
            // TODO: Initialize community-specific dependencies when implemented
        }

        public override void StartCall(Web3Address userAddress)
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Community voice chat StartCall not yet implemented");
        }

        public override void HangUp()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Community voice chat HangUp not yet implemented");
        }

        public override void HandleLivekitConnectionFailed()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Community voice chat HandleLivekitConnectionFailed not yet implemented");
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
        }
    }
}
