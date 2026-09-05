using System.Collections.Generic;
// ReSharper disable InconsistentNaming

namespace DCL.VoiceChat.Services
{
    public struct ActiveCommunityVoiceChat
    {
        public string communityId;
        public string communityName;
        public string? communityImage;
        public bool isMember;
        public List<string> positions;
        public List<string> worlds;
        public int participantCount;
        public int moderatorCount;
    }

    public struct ActiveCommunityVoiceChatsData
    {
        public List<ActiveCommunityVoiceChat> activeChats;
        public int total;
    }

    public struct ActiveCommunityVoiceChatsResponse
    {
        public ActiveCommunityVoiceChatsData data;
    }
}
