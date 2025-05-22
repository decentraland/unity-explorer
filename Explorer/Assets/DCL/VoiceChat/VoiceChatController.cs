using System;

namespace DCL.VoiceChat
{
    public class VoiceChatController : IDisposable
    {
        private readonly VoiceChatView view;

        public VoiceChatController(VoiceChatView view)
        {
            this.view = view;

            view.IncomingCallView.AcceptCallButton.onClick.AddListener(AcceptCall);
            view.IncomingCallView.RefuseCallButton.onClick.AddListener(RefuseCall);

            view.OutgoingCallView.MicrophoneButton.onClick.AddListener(ToggleMicrophone);
            view.OutgoingCallView.HangUpButton.onClick.AddListener(HangUp);

            view.InCallView.MicrophoneButton.onClick.AddListener(ToggleMicrophone);
            view.InCallView.HangUpButton.onClick.AddListener(HangUp);
        }

        public void Show()
        {

        }

        public void Hide()
        {

        }

        private void HangUp()
        {

        }

        private void ToggleMicrophone()
        {

        }

        private void RefuseCall()
        {

        }

        private void AcceptCall()
        {

        }

        public void Dispose()
        {

        }
    }
}
