using System;

namespace DCL.VoiceChat
{
    public class VoiceChatController : IDisposable
    {
        private readonly VoiceChatView view;
        private readonly VoiceChatCallStatusService voiceChatCallStatusService;

        public VoiceChatController(VoiceChatView view, VoiceChatCallStatusService voiceChatCallStatusService)
        {
            this.view = view;
            this.voiceChatCallStatusService = voiceChatCallStatusService;

            view.IncomingCallView.AcceptCallButton.onClick.AddListener(AcceptCall);
            view.IncomingCallView.RefuseCallButton.onClick.AddListener(RefuseCall);

            view.OutgoingCallView.MicrophoneButton.onClick.AddListener(ToggleMicrophone);
            view.OutgoingCallView.HangUpButton.onClick.AddListener(HangUp);

            view.InCallView.MicrophoneButton.onClick.AddListener(ToggleMicrophone);
            view.InCallView.HangUpButton.onClick.AddListener(HangUp);

            this.voiceChatCallStatusService.StatusChanged += OnVoiceChatStatusChanged;
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus status)
        {
            if (status is VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDED_CALL)
                Hide();
            else
                Show();

            view.SetActiveSection(status);
        }

        public void Show()
        {
            view.VoiceChatContainer.SetActive(true);
        }

        public void Hide()
        {
            view.VoiceChatContainer.SetActive(false);
        }

        private void HangUp()
        {
            voiceChatCallStatusService.StopCall();
        }

        private void ToggleMicrophone()
        {

        }

        private void RefuseCall()
        {
            voiceChatCallStatusService.StopCall();
        }

        private void AcceptCall()
        {

        }

        public void Dispose()
        {
            this.voiceChatCallStatusService.StatusChanged -= OnVoiceChatStatusChanged;
        }
    }
}
