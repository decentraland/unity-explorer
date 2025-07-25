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
        private readonly IDisposable isSpeakerSubscription;
        private readonly IDisposable isRequestingToSpeakSubscription;
        private readonly IDisposable callStateSubscription;

        private readonly CommunityVoiceChatInCallFooterView view;
        private readonly IVoiceChatOrchestrator orchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private readonly MicrophoneButtonController microphoneButtonController;
        private readonly IDisposable? playerStateSubscription;


        private CancellationTokenSource communityCts = new ();
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

            isRequestingToSpeakSubscription = orchestrator.ParticipantsStateService.LocalParticipantState.IsRequestingToSpeak.Subscribe(OnRequestingToSpeakChanged);
            isSpeakerSubscription = orchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Subscribe(OnIsSpeakerChanged);
            callStateSubscription = orchestrator.CurrentCallStatus.Subscribe(OnCallStateChanged);

            view.LeaveStageButton.gameObject.SetActive(false);
            view.MicrophoneButton.gameObject.SetActive(false);
            view.RaiseHandButton.gameObject.SetActive(false);

            //We also need to initialize the UI properly when a call is active.
        }

        private void OnCallStateChanged(VoiceChatStatus callStatus)
        {
            if (orchestrator.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            if (callStatus != VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            bool isSpeaker = orchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Value;
            OnIsSpeakerChanged(isSpeaker);
        }

        private void OnIsSpeakerChanged(bool isSpeaker)
        {
            OnIsSpeakerChangedAsync(isSpeaker).Forget();
        }

        private async UniTaskVoid OnIsSpeakerChangedAsync(bool isSpeaker)
        {
            //We need async methods as these happen because of BE responses or Livekit events
            await UniTask.SwitchToMainThread();
            view.LeaveStageButton.gameObject.SetActive(isSpeaker);
            view.MicrophoneButton.gameObject.SetActive(isSpeaker);
            view.RaiseHandButton.gameObject.SetActive(!isSpeaker);
        }

        private void OnRequestingToSpeakChanged(bool isRequestingToSpeak)
        {
            OnRequestingToSpeakChangedAsync(isRequestingToSpeak).Forget();
            //For now hide button after user requests to speak just to see it in action
            //Change button appearance? Disable Clicking again? should clicking again cancel our request to speak?
        }

        private async UniTaskVoid OnRequestingToSpeakChangedAsync(bool isRequestingToSpeak)
        {
            //We need async methods as these happen because of BE responses or Livekit events
            await UniTask.SwitchToMainThread();
            view.RaiseHandButton.gameObject.SetActive(!isRequestingToSpeak);
        }

        public void Dispose()
        {
            isSpeakerSubscription?.Dispose();
            isRequestingToSpeakSubscription?.Dispose();
            callStateSubscription?.Dispose();
            cts?.Dispose();
            communityCts?.Dispose();
        }

        private void OnRaiseHandButtonClicked()
        {
            orchestrator.CommunityStatusService.RequestToSpeakInCurrentCall();
        }

        private void OnLeaveStageButtonClicked()
        {
            orchestrator.CommunityStatusService.DemoteFromSpeakerInCurrentCall(orchestrator.ParticipantsStateService.LocalParticipantId);
        }

        private void OnEndCallButtonClicked()
        {
            cts = cts?.SafeRestart();
            orchestrator.HangUp();
        }

    }
}
