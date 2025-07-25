using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Utilities;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallFooterController : IDisposable
    {
        private readonly IDisposable statusSubscription;
        private readonly IDisposable currentChannelSubscription;

        private readonly CommunityVoiceChatInCallFooterView view;
        private readonly IVoiceChatOrchestrator orchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private readonly MicrophoneButtonController microphoneButtonController;

        private readonly CancellationTokenSource communityCts = new ();
        private CancellationTokenSource cts = new ();

        public CommunityVoiceChatInCallFooterController(
            CommunityVoiceChatInCallFooterView view,
            IVoiceChatOrchestrator orchestrator)
        {
            this.view = view;
            this.orchestrator = orchestrator;

            // We need to refactor microphoneController to either let us add new views to it dynamically and let it manage all buttons
            // or use a shared reactive property for the microphone state that all views subscribe to
            // Disabled for now.
            // this.microphoneButtonController = new MicrophoneButtonController(view.MicrophoneButton, microphoneHandler);

            this.view.EndCallButton.onClick.AddListener(OnEndCallButtonClicked);
            this.view.LeaveStageButton.onClick.AddListener(OnLeaveStageButtonClicked);
            this.view.RaiseHandButton.onClick.AddListener(OnRaiseHandButtonClicked);

            // I think we should manage this here and send an event through a bus like the new chat thing is doing? so each controller listens to its own view.
            // view.OpenListenersSectionButton
            //We need to listen to our own user state to properly update the UI.
        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            cts?.Dispose();
            communityCts?.Dispose();
        }

        private void OnRaiseHandButtonClicked()
        {
            orchestrator.CommunityStatusService.RequestToSpeakInCurrentCall();

            // We send the request and need to change the button appearance I guess + our state?
            // Unless we react to metadata changes directly then we just subscribe to our own request to speak status?
            // Might be cleaner that way? but maybe a little less responsive? we could change our status directly so the event is triggered immediately maybe??
            // In this case we would need to send our state with the request? so the service can update it? We might need to create an interface that only the Community service can access
            // To avoid views from changing states directly.
        }

        private void OnLeaveStageButtonClicked()
        {
            orchestrator.CommunityStatusService.DemoteFromSpeakerInCurrentCall(orchestrator.ParticipantsStateService.LocalParticipantId);

            //Again react based on BE response, dont do anything else here
        }

        private void OnEndCallButtonClicked()
        {
            cts = cts?.SafeRestart();
            HandleCallButtonClickAsync(cts!.Token).Forget();
        }

        private async UniTaskVoid HandleCallButtonClickAsync(CancellationToken ct) { }
    }
}
