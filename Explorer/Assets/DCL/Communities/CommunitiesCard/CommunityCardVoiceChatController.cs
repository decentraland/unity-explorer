using DCL.Audio;
using DCL.VoiceChat;
using System;
using System.Text;
using System.Threading;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardVoiceChatController : IDisposable
    {
        public event Action ClosePanel;
        private readonly CommunityCardVoiceChatView view;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly StringBuilder stringBuilder = new ();

        private string currentCommunityId;

        public CommunityCardVoiceChatController(CommunityCardVoiceChatView view, IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

            currentCommunityId = string.Empty;

            view.StartStreamButton.onClick.AddListener(StartStream);
            view.JoinStreamButton.onClick.AddListener(JoinStream);
            view.ListeningButton.onClick.AddListener(GoToStream);

            voiceChatOrchestrator.CurrentCommunityId.OnUpdate += UpdateJoinLeaveButtonState;
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

        public void SetPanelStatus(bool isStreamRunning, bool isModOrAdmin, string communityId)
        {
            currentCommunityId = communityId;
            view.VoiceChatPanel.SetActive(isStreamRunning || isModOrAdmin);
            view.ModeratorControlPanel.SetActive(!isStreamRunning && isModOrAdmin);
            view.LiveStreamPanel.SetActive(isStreamRunning);

            UpdateJoinLeaveButtonState(currentCommunityId);
        }

        public void SetListenersCount(int listenersCount)
        {
            stringBuilder.Clear();
            stringBuilder.Append(listenersCount);
            stringBuilder.Append(" Listening");
            view.ListenersCount.text = stringBuilder.ToString();
        }

        public void Dispose()
        {
            voiceChatOrchestrator.CurrentCommunityId.OnUpdate -= UpdateJoinLeaveButtonState;
        }
    }
}
