using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
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
        private readonly IChatEventBus chatEventBus;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private CancellationTokenSource communityCts = new ();

        private CancellationTokenSource cts;

        public CommunityVoiceChatInCallFooterController(
            CommunityVoiceChatInCallFooterView view,
            IVoiceChatOrchestrator orchestrator,
            IChatEventBus chatEventBus)
        {
            this.view = view;
            this.orchestrator = orchestrator;
            this.chatEventBus = chatEventBus;

            this.view.EndCallButton.onClick.AddListener(OnEndCallButtonClicked);
            this.view.LeaveStageButton.onClick.AddListener(OnLeaveStageButtonClicked);
            this.view.RaiseHandButton.onClick.AddListener(OnRaiseHandButtonClicked);

            // Initialize MicrophoneButtonController here, we need to pass the dependencies.
            //MicrophoneButtonController microphoneButtonController = new MicrophoneButtonController();

            // I think we should manage this here and send an event through a bus like the new chat thing is doing? so each controller listens to its own view.
            // view.OpenListenersSectionButton

            cts = new CancellationTokenSource();


            //We need to listen to our own user state to properly update the UI.
        }

        private void OnRaiseHandButtonClicked()
        {
            orchestrator.CommunityStatusService.RequestToSpeakInCurrentCall();
            // We send the request and need to change the button appearance I guess + our state?
            // Unless we react to metadata changes directly then we just subscribe to our own request to speak status?
            // Might be cleaner that way? but maybe a little less responsive? we could change our status directly so the event is triggered immediately maybe??
        }

        private void OnLeaveStageButtonClicked()
        {
            orchestrator.CommunityStatusService.DemoteFromSpeakerInCurrentCall(orchestrator.ParticipantsStateService.LocalParticipantId);
            //Again react based on BE response, dont do anything else here
        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            cts?.Dispose();
            communityCts?.Dispose();
        }

        private void OnEndCallButtonClicked()
        {
            cts = cts?.SafeRestart();
            HandleCallButtonClickAsync(cts!.Token).Forget();
        }

        private async UniTaskVoid HandleCallButtonClickAsync(CancellationToken ct)
        {
        }


    }
}
