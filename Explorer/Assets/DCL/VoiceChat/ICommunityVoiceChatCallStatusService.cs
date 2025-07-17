namespace DCL.VoiceChat
{
    /// <summary>
    /// Interface for community voice chat call status service that exposes community calls specific properties
    /// </summary>
    public interface ICommunityVoiceChatCallStatusService
    {
        /// <summary>
        /// Checks if a community has an active voice chat call
        /// </summary>
        /// <param name="communityId">The community ID to check</param>
        /// <param name="callId">The call ID if the community has an active call, null otherwise</param>
        /// <returns>True if the community has an active voice chat call, false otherwise</returns>
        bool HasActiveVoiceChatCall(string communityId, out string? callId);
    }
}
