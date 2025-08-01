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
        private readonly IDisposable microphoneStateSubscription;

        private readonly CommunityVoiceChatInCallFooterView view;
        private readonly IVoiceChatOrchestrator orchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;

        private CancellationTokenSource cts = new ();

        public CommunityVoiceChatInCallFooterController(
            CommunityVoiceChatInCallFooterView view,
            IVoiceChatOrchestrator orchestrator,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.view = view;
            this.orchestrator = orchestrator;
            this.microphoneHandler = microphoneHandler;

            this.view.EndCallButton.onClick.AddListener(OnEndCallButtonClicked);
            this.view.LeaveStageButton.onClick.AddListener(OnLeaveStageButtonClicked);
            this.view.RaiseHandButton.onClick.AddListener(OnRaiseHandButtonClicked);
            this.view.MicrophoneButton.MicButton.onClick.AddListener(OnMicrophoneButtonClicked);

            isRequestingToSpeakSubscription = orchestrator.ParticipantsStateService.LocalParticipantState.IsRequestingToSpeak.Subscribe(OnRequestingToSpeakChanged);
            isSpeakerSubscription = orchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Subscribe(OnIsSpeakerChanged);
            callStateSubscription = orchestrator.CurrentCallStatus.Subscribe(OnCallStateChanged);

            microphoneStateSubscription = microphoneHandler.IsMicrophoneEnabled.Subscribe(OnMicrophoneStateChanged);

            view.MicrophoneButton.SetMicrophoneStatus(microphoneHandler.IsMicrophoneEnabled.Value);
            view.LeaveStageButton.gameObject.SetActive(false);
            view.MicrophoneButton.gameObject.SetActive(false);
            view.RaiseHandButton.gameObject.SetActive(false);
        }

        private void OnMicrophoneButtonClicked()
        {
            microphoneHandler.ToggleMicrophone();
        }

        private void OnMicrophoneStateChanged(bool isEnabled)
        {
            view.MicrophoneButton.SetMicrophoneStatus(isEnabled);
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
            if (!PlayerLoopHelper.IsMainThread)
            {
                OnIsSpeakerChangedAsync().Forget();
                return;
            }

            OnIsSpeakerChangedInternal();
            return;

            void OnIsSpeakerChangedInternal()
            {
                view.LeaveStageButton.gameObject.SetActive(isSpeaker);
                view.MicrophoneButton.gameObject.SetActive(isSpeaker);
                view.RaiseHandButton.gameObject.SetActive(!isSpeaker);
                if (isSpeaker)
                    view.MicrophoneButton.SetMicrophoneStatus(microphoneHandler.IsMicrophoneEnabled.Value);
            }
            async UniTaskVoid OnIsSpeakerChangedAsync()
            {
                await UniTask.SwitchToMainThread();
                OnIsSpeakerChangedInternal();
            }
        }

        private void OnRequestingToSpeakChanged(bool isRequestingToSpeak)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                OnRequestingToSpeakChangedAsync().Forget();
                return;
            }

            OnRequestingToSpeakChangedInternal();
            return;

            void OnRequestingToSpeakChangedInternal()
            {
                view.RaiseHandButton.gameObject.SetActive(!isRequestingToSpeak);
            }

            async UniTaskVoid OnRequestingToSpeakChangedAsync()
            {
                await UniTask.SwitchToMainThread();
                OnRequestingToSpeakChangedInternal();
            }
        }


        public void Dispose()
        {
            isSpeakerSubscription?.Dispose();
            isRequestingToSpeakSubscription?.Dispose();
            callStateSubscription?.Dispose();
            microphoneStateSubscription?.Dispose();
            cts?.Dispose();
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
