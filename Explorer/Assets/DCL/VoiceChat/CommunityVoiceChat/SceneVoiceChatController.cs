#nullable enable
using DCL.Communities;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class SceneVoiceChatController : IDisposable
    {
        private readonly SceneVoiceChatTitlebarView view;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly IDisposable currentSceneActiveCallSubscription;
        private readonly IDisposable currentCallStatusSubscription;


        public SceneVoiceChatController(SceneVoiceChatTitlebarView view, CommunitiesDataProvider communityDataProvider, IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.communityDataProvider = communityDataProvider;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            currentSceneActiveCallSubscription = voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Subscribe(OnActiveCommunityChanged);
            currentCallStatusSubscription = voiceChatOrchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status is not (VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)) return;

            if (voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Value != null)
            {
                view.VoiceChatContainer.gameObject.SetActive(true);
            }
        }

        private void OnActiveCommunityChanged(ActiveCommunityVoiceChat? activeCommunityVoiceChat)
        {
            if (voiceChatOrchestrator.CurrentCallStatus.Value is not (VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)) return;

            view.VoiceChatContainer.gameObject.SetActive(true);
        }

        public void Dispose()
        {
        }
    }
}
