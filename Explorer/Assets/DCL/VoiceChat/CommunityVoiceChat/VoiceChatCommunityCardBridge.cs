using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    /// <summary>
    ///     Simple bridge to open community card from voice chat without creating cyclic dependencies.
    ///     This follows the same pattern as other bridges in the codebase.
    /// </summary>
    public static class VoiceChatCommunityCardBridge
    {
        private static Action<string>? openCommunityCardAction;

        public static void SetOpenCommunityCardAction(Action<string> action)
        {
            openCommunityCardAction = action;
        }

        public static void OpenCommunityCard(string communityId)
        {
            if (string.IsNullOrEmpty(communityId))
                return;

            openCommunityCardAction?.Invoke(communityId);
        }
    }
}
