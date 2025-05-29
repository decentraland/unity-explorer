
using Cysharp.Threading.Tasks;
using DCL.Web3;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

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

            micController = new MicrophoneButtonController(list, microphoneHandler);

            this.voiceChatCallStatusService.StatusChanged += OnVoiceChatStatusChanged;
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus status, Web3Address walletId)
        {
            if (status is VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDING_CALL)
                Hide();
            else
                Show();

            view.SetActiveSection(status, walletId);
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
            micController.Dispose();
        }
    }
}
