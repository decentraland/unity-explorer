#nullable enable
using DCL.Communities;
using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class SceneVoiceChatController : IDisposable
    {
        private readonly SceneVoiceChatTitlebarView view;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;

        public SceneVoiceChatController(SceneVoiceChatTitlebarView view, CommunitiesDataProvider communityDataProvider, IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.communityDataProvider = communityDataProvider;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
        }

        public void Dispose()
        {
        }
    }
}
