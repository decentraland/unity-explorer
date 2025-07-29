using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatSearchController : IDisposable
    {
        private readonly CommunityVoiceChatSearchView view;
        private int listenersCount = 0;

        public CommunityVoiceChatSearchController(CommunityVoiceChatSearchView view)
        {
            this.view = view;
        }

        public void AddListener()
        {
            listenersCount++;
        }

        public void Dispose()
        {
        }
    }
}
