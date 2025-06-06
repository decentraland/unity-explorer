using DCL.Audio;
using DCL.Web3;
using MVC;
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

        public VoiceChatController(
            VoiceChatView view,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            VoiceChatMicrophoneHandler microphoneHandler,
            ViewDependencies dependencies)
        {
            this.view = view;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.microphoneHandler = microphoneHandler;

            view.IncomingCallView.AcceptCallButton.onClick.AddListener(AcceptCall);
            view.IncomingCallView.RefuseCallButton.onClick.AddListener(RefuseCall);
            view.IncomingCallView.ProfileView.InjectDependencies(dependencies);

            view.OutgoingCallView.MicrophoneButton.MicButton.onClick.AddListener(ToggleMicrophone);
            view.OutgoingCallView.HangUpButton.onClick.AddListener(HangUp);
            view.OutgoingCallView.ProfileView.InjectDependencies(dependencies);

            view.InCallView.MicrophoneButton.MicButton.onClick.AddListener(ToggleMicrophone);
            view.InCallView.HangUpButton.onClick.AddListener(HangUp);
            view.InCallView.ProfileView.InjectDependencies(dependencies);

            var list = new List<MicrophoneButton>
            {
                view.OutgoingCallView.MicrophoneButton,
                view.InCallView.MicrophoneButton,
            };

            micController = new MicrophoneButtonController(list, microphoneHandler, view.MuteMicrophoneAudio, view.UnMuteMicrophoneAudio);

            this.voiceChatCallStatusService.StatusChanged += OnVoiceChatStatusChanged;
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus status)
        {
            if (status is VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDING_CALL)
                view.Hide(status, this.voiceChatCallStatusService.CurrentTargetWallet);
            else
                view.Show(status, this.voiceChatCallStatusService.CurrentTargetWallet);

        }

        private void HangUp()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.LeaveCallAudio);
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
            voiceChatCallStatusService.StartCall(new Web3Address());
        }

        public void Dispose()
        {
            this.voiceChatCallStatusService.StatusChanged -= OnVoiceChatStatusChanged;
            micController.Dispose();
        }
    }
}
