using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    [Serializable]
    public class VoiceChatMember
    {
        public string WalletId;
        public bool IsModerator;
        public bool IsSpeaker;
    }
}
