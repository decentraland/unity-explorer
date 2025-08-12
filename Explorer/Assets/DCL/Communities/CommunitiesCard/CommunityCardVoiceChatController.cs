using DCL.VoiceChat;
using System;
using System.Text;
using System.Threading;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardVoiceChatController
    {
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
            view.EndStreamButton.onClick.AddListener(EndStream);
            view.JoinStreamButton.onClick.AddListener(JoinStream);
            view.LeaveStreamButton.onClick.AddListener(LeaveStream);
        }

        private void EndStream()
        {
            voiceChatOrchestrator.EndStreamInCurrentCall();
            SetPanelStatus(false, true, currentCommunityId);
        }

        private void LeaveStream()
        {
            voiceChatOrchestrator.HangUp();
            SetPanelStatus(true, false, currentCommunityId);
        }

        private void JoinStream()
        {
            voiceChatOrchestrator.JoinCommunityVoiceChat(currentCommunityId, new CancellationToken());
            SetPanelStatus(true, false, currentCommunityId);
        }

        private void StartStream()
        {
            voiceChatOrchestrator.StartCall(currentCommunityId, VoiceChatType.COMMUNITY);
            SetPanelStatus(true, true, currentCommunityId);
        }

        public void SetPanelStatus(bool isStreamRunning, bool isModOrAdmin, string communityId)
        {
            currentCommunityId = communityId;
            view.VoiceChatPanel.SetActive(isStreamRunning || isModOrAdmin);
            view.ModeratorControlPanel.SetActive(!isStreamRunning && isModOrAdmin);
            view.LiveStreamPanel.SetActive(isStreamRunning);

            view.LeaveStreamButton.gameObject.SetActive(voiceChatOrchestrator.CurrentCommunityId.Value == currentCommunityId);
            view.JoinStreamButton.gameObject.SetActive(voiceChatOrchestrator.CurrentCommunityId.Value != currentCommunityId);
            view.StartStreamButton.gameObject.SetActive(!isStreamRunning);
            view.EndStreamButton.gameObject.SetActive(isStreamRunning);
        }

        public void SetListenersCount(int listenersCount)
        {
            stringBuilder.Clear();
            stringBuilder.Append(listenersCount);
            stringBuilder.Append(" Listening");
            view.ListenersCount.text = stringBuilder.ToString();
        }
    }
}
