using DCL.Audio;
using DCL.FeatureFlags;
using DCL.VoiceChat;
using System;
using System.Text;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardVoiceChatPresenter : IDisposable
    {
        public event Action? ClosePanel;

        private readonly CommunityCardVoiceChatView view;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly StringBuilder stringBuilder = new ();

        private string currentCommunityId;

        public CommunityCardVoiceChatPresenter(CommunityCardVoiceChatView view, IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

            currentCommunityId = string.Empty;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
            {
                view.StartStreamButton.onClick.AddListener(StartStream);
                view.JoinStreamButton.onClick.AddListener(JoinStream);
                view.ListeningButton.onClick.AddListener(GoToStream);
                voiceChatOrchestrator.CurrentCommunityId.OnUpdate += UpdateJoinLeaveButtonState;
            }
            else
            {
                view.gameObject.SetActive(false);
            }
        }

        private void UpdateJoinLeaveButtonState(string communityId)
        {
            view.ListeningButton.gameObject.SetActive(communityId == currentCommunityId);
            view.JoinStreamButton.gameObject.SetActive(communityId != currentCommunityId);
            view.HandleListeningAnimation(communityId == currentCommunityId);
        }

        private void GoToStream()
        {
            ClosePanel?.Invoke();
        }

        private void JoinStream()
        {
            ClosePanel?.Invoke();
            voiceChatOrchestrator.JoinCommunityVoiceChat(currentCommunityId,true);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.StartStreamAudio);
            SetPanelStatus(true, false, currentCommunityId);
        }

        private void StartStream()
        {
            ClosePanel?.Invoke();
            voiceChatOrchestrator.StartCall(currentCommunityId, VoiceChatType.COMMUNITY);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.StartStreamAudio);
            SetPanelStatus(true, true, currentCommunityId);
        }

        public void Reset()
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                return;

            view.VoiceChatPanel.SetActive(false);
        }

        public void SetPanelStatus(bool isStreamRunning, bool isModOrAdmin, string communityId)
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                return;

            currentCommunityId = communityId;

            view.VoiceChatPanel.SetActive(isStreamRunning || isModOrAdmin);
            view.ModeratorControlPanel.SetActive(!isStreamRunning && isModOrAdmin);
            view.LiveStreamPanel.SetActive(isStreamRunning);

            UpdateJoinLeaveButtonState(voiceChatOrchestrator.CurrentCommunityId.Value);
        }

        public void SetListenersCount(int listenersCount)
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                return;

            stringBuilder.Clear();
            stringBuilder.Append(listenersCount);
            stringBuilder.Append(" Listening");
            view.ListenersCount.text = stringBuilder.ToString();
        }

        public void Dispose()
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                return;

            voiceChatOrchestrator.CurrentCommunityId.OnUpdate -= UpdateJoinLeaveButtonState;
            view.StartStreamButton.onClick.RemoveListener(StartStream);
            view.JoinStreamButton.onClick.RemoveListener(JoinStream);
            view.ListeningButton.onClick.RemoveListener(GoToStream);
        }
    }
}
