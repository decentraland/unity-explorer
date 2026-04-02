namespace DCL.VoiceChat
{
    /// <summary>
    /// Static helper for VoiceChatParticipantCommunityRole operations and validations.
    /// </summary>
    public static class VoiceChatRoleHelper
    {
        public static bool IsModeratorOrOwner(VoiceChatParticipantCommunityRole role) =>
            role is VoiceChatParticipantCommunityRole.MODERATOR or VoiceChatParticipantCommunityRole.OWNER;
    }
}
