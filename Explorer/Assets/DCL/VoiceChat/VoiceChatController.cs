using DCL.Audio;
using DCL.UI.Profiles.Helpers;
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
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private readonly MicrophoneButtonController micController;

        public VoiceChatController(
            VoiceChatView view,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            VoiceChatMicrophoneHandler microphoneHandler,
            ViewDependencies dependencies,
            ProfileRepositoryWrapper profileDataProvider)
        {
            this.view = view;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.microphoneHandler = microphoneHandler;
            this.profileDataProvider = profileDataProvider;

            view.IncomingCallView.AcceptCallButton.onClick.AddListener(AcceptCall);
            view.IncomingCallView.RefuseCallButton.onClick.AddListener(RefuseCall);
            view.IncomingCallView.ProfileView.InjectDependencies(dependencies);

            view.OutgoingCallView.HangUpButton.onClick.AddListener(HangUp);
            view.OutgoingCallView.ProfileView.InjectDependencies(dependencies);

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
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_STARTING_CALL or VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL:
                    view.Show();
                    break;
                case VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                    view.Hide();
                    break;
            }

            if (status is VoiceChatStatus.VOICE_CHAT_STARTED_CALL or VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL)
                UIAudioEventsBus.Instance.SendPlayContinuousAudioEvent(view.CallTuneAudio);
            else
                UIAudioEventsBus.Instance.SendStopPlayingContinuousAudioEvent(view.CallTuneAudio);

            view.SetActiveSection(status, voiceChatCallStatusService.CurrentTargetWallet, profileDataProvider);
        }

        private void HangUp()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.LeaveCallAudio);
            voiceChatCallStatusService.HangUp();
        }

        private void RefuseCall()
        {
            voiceChatCallStatusService.RejectCall();
        }

        private void AcceptCall()
        {
            voiceChatCallStatusService.AcceptCall();
        }

        public void Dispose()
        {
            this.voiceChatCallStatusService.StatusChanged -= OnVoiceChatStatusChanged;
            micController.Dispose();
        }
    }
}
