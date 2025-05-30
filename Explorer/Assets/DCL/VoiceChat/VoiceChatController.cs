using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    public class VoiceChatController : IDisposable
    {
        private readonly VoiceChatView view;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly MicrophoneButtonController micController;

        public VoiceChatController(VoiceChatView view, IVoiceChatCallStatusService voiceChatCallStatusService, VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.view = view;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.microphoneHandler = microphoneHandler;

            view.IncomingCallView.AcceptCallButton.onClick.AddListener(AcceptCall);
            view.IncomingCallView.RefuseCallButton.onClick.AddListener(RefuseCall);

            view.OutgoingCallView.MicrophoneButton.MicButton.onClick.AddListener(ToggleMicrophone);
            view.OutgoingCallView.HangUpButton.onClick.AddListener(HangUp);

            view.InCallView.MicrophoneButton.MicButton.onClick.AddListener(ToggleMicrophone);
            view.InCallView.HangUpButton.onClick.AddListener(HangUp);

            var list = new List<MicrophoneButton>
            {
                view.OutgoingCallView.MicrophoneButton,
                view.InCallView.MicrophoneButton,
            };

            micController = new MicrophoneButtonController(list, microphoneHandler);

            this.voiceChatCallStatusService.StatusChanged += OnVoiceChatStatusChanged;
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus status)
        {
            if (status is VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDING_CALL)
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
            microphoneHandler.ToggleMicrophone();
        }

        private void RefuseCall()
        {
            voiceChatCallStatusService.StopCall();
        }

        private void AcceptCall()
        {
            voiceChatCallStatusService.StartCall("");
        }

        public void Dispose()
        {
            this.voiceChatCallStatusService.StatusChanged -= OnVoiceChatStatusChanged;
            micController.Dispose();
        }
    }
}
