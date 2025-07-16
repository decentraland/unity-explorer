using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatSearchController : IDisposable
    {
        private readonly CommunityVoiceChatSearchView view;

        public CommunityVoiceChatSearchController(CommunityVoiceChatSearchView view)
        {
            this.view = view;
        }

        public void Dispose()
        {
        }
    }
}
