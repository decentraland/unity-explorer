using DCL.Utilities;
using DCL.VoiceChat.Services;
using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class SceneVoiceChatController : IDisposable
    {
        private readonly SceneVoiceChatTitlebarView view;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly IDisposable currentSceneActiveCallSubscription;
        private readonly IDisposable currentCallStatusSubscription;

        public SceneVoiceChatController(
            SceneVoiceChatTitlebarView view,
            IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            currentSceneActiveCallSubscription = voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Subscribe(OnActiveCommunityChanged);
            currentCallStatusSubscription = voiceChatOrchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
            view.SceneVoiceChatActiveCallView.JoinStreamButton.onClick.AddListener(OnJoinStreamClicked);
        }

        private void OnJoinStreamClicked()
        {
            if (voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Value != null)
                voiceChatOrchestrator.JoinCommunityVoiceChat(voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Value.Value.communityId, true);
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status is not (VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR))
            {
                view.VoiceChatContainer.gameObject.SetActive(false);
                return;
            }

            var communityData = voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Value;

            if (communityData != null)
            {
                view.VoiceChatContainer.gameObject.SetActive(true);
                view.SceneVoiceChatActiveCallView.SetCommunityName(communityData.Value.communityName);
                view.SceneVoiceChatActiveCallView.SetParticipantCount(communityData.Value.participantCount);
            }
        }

        private void OnActiveCommunityChanged(ActiveCommunityVoiceChat? activeCommunityVoiceChat)
        {
            if (voiceChatOrchestrator.CurrentCallStatus.Value is not (VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR))
            {
                view.VoiceChatContainer.gameObject.SetActive(false);
                return;
            }

            if (activeCommunityVoiceChat != null)
            {
                view.VoiceChatContainer.gameObject.SetActive(true);
                view.SceneVoiceChatActiveCallView.SetCommunityName(activeCommunityVoiceChat.Value.communityName);
                view.SceneVoiceChatActiveCallView.SetParticipantCount(activeCommunityVoiceChat.Value.participantCount);
            }
            else
            {
                view.VoiceChatContainer.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            currentSceneActiveCallSubscription.Dispose();
            currentCallStatusSubscription.Dispose();
        }
    }
}
