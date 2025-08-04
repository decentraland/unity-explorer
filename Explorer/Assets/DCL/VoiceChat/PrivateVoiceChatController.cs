using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class PrivateVoiceChatController : IDisposable
    {
        private readonly VoiceChatView view;
        private readonly IPrivateCallOrchestrator privateCallOrchestrator;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private readonly IRoom voiceChatRoom;
        private readonly MicrophoneButtonController micController;
        private readonly IDisposable? statusSubscription;

        private CancellationTokenSource cts;

        public PrivateVoiceChatController(
            VoiceChatView view,
            IVoiceChatOrchestrator privateCallOrchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            ProfileRepositoryWrapper profileDataProvider,
            IRoom voiceChatRoom)
        {
            this.view = view;
            this.privateCallOrchestrator = privateCallOrchestrator;
            this.profileDataProvider = profileDataProvider;
            this.voiceChatRoom = voiceChatRoom;

            view.IncomingCallView.AcceptCallButton.onClick.AddListener(AcceptCall);
            view.IncomingCallView.RefuseCallButton.onClick.AddListener(RefuseCall);

            view.OutgoingCallView.HangUpButton.onClick.AddListener(HangUp);

            view.InCallView.HangUpButton.onClick.AddListener(HangUp);


            var list = new List<MicrophoneButton>
            {
                view.OutgoingCallView.MicrophoneButton,
                view.InCallView.MicrophoneButton,
            };

            micController = new MicrophoneButtonController(list, microphoneHandler);

            statusSubscription = this.privateCallOrchestrator.PrivateCallStatus.Subscribe(OnPrivateVoiceChatStatusChanged);
            this.voiceChatRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            this.voiceChatRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            this.voiceChatRoom.ConnectionUpdated += OnConnectionUpdated;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            if (connectionUpdate == ConnectionUpdate.Connected)
                view.SetInCallSection();
        }

        private void OnActiveSpeakersUpdated()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            OnActiveSpeakersUpdatedAsync(cts.Token).Forget();
        }

        private async UniTaskVoid OnActiveSpeakersUpdatedAsync(CancellationToken ct)
        {
            string userName = string.Empty;
            await UniTask.SwitchToMainThread();

            if (voiceChatRoom.ActiveSpeakers.Count == 1)
            {
                foreach (string activeSpeaker in voiceChatRoom.ActiveSpeakers)
                {
                    Profile profileAsync = await profileDataProvider.GetProfileAsync(activeSpeaker, ct);
                    if (profileAsync != null) userName = profileAsync.Name;
                }
            }

            view.SetSpeakingStatus(voiceChatRoom.ActiveSpeakers.Count, userName);
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {

        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
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

            view.SetActiveSection(status, privateCallOrchestrator.CurrentTargetWallet, profileDataProvider);
        }

        private void HangUp()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.LeaveCallAudio);
            privateCallOrchestrator.HangUp();
        }

        private void RefuseCall()
        {
            privateCallOrchestrator.RejectCall();
        }

        private void AcceptCall()
        {
            privateCallOrchestrator.AcceptPrivateCall();
        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
            this.voiceChatRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            this.voiceChatRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            micController.Dispose();
        }
    }
}
