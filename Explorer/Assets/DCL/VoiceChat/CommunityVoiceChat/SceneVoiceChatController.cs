#nullable enable
using DCL.Communities;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class SceneVoiceChatController : IDisposable
    {
        private readonly SceneVoiceChatTitlebarView view;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly IDisposable currentSceneActiveCallSubscription;
        private readonly IDisposable currentCallStatusSubscription;

        private CancellationTokenSource cts = new ();

        public SceneVoiceChatController(
            SceneVoiceChatTitlebarView view,
            CommunitiesDataProvider communityDataProvider,
            IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.communityDataProvider = communityDataProvider;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            currentSceneActiveCallSubscription = voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Subscribe(OnActiveCommunityChanged);
            currentCallStatusSubscription = voiceChatOrchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
            view.SceneVoiceChatActiveCallView.JoinStreamButton.onClick.AddListener(OnJoinStreamClicked);
        }

        private void OnJoinStreamClicked()
        {
            cts = cts.SafeRestart();
            if (voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Value != null)
                voiceChatOrchestrator.JoinCommunityVoiceChat(voiceChatOrchestrator.CurrentSceneActiveCommunityVoiceChatData.Value.Value.communityId, cts.Token);
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status is not (VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR))
            {
                view.VoiceChatContainer.gameObject.SetActive(false);
                return;
            };

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
            };

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
