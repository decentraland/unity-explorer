using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    ///     Orchestrates proximity voice chat: coordinates state transitions
    ///     (Hearing/Speaking/Suppressed/Disabled) and delegates microphone publishing
    ///     to <see cref="MicrophoneTrackPublisher"/> and remote track management
    ///     to <see cref="ProximityRemoteTrackListener"/>.
    ///     Mic control (PTT, device switching) is handled by <see cref="VoiceChatMicrophoneHandler"/>.
    /// </summary>
    public class ProximityVoiceChatManager : IDisposable
    {
        private const string TAG = nameof(ProximityVoiceChatManager);

        private readonly VoiceChatConfiguration configuration;
        private readonly IRoom islandRoom;
        private readonly ProximityVoiceChatStateModel stateModel;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly MicrophoneTrackPublisher micPublisher;
        private readonly ProximityRemoteTrackListener remoteListener;

        private readonly IDisposable callStatusSubscription;
        private readonly IDisposable? proximityStateSubscription;

        private CancellationTokenSource activationCts = new ();

        private bool disposed;

        public ProximityVoiceChatManager(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            ConcurrentDictionary<string, AudioSource> activeAudioSources,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus,
            ProximityVoiceChatStateModel stateModel,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.stateModel = stateModel;
            this.microphoneHandler = microphoneHandler;

            micPublisher = new MicrophoneTrackPublisher(islandRoom, configuration, microphoneHandler, VoiceChatType.PROXIMITY);
            remoteListener = new ProximityRemoteTrackListener(islandRoom, configuration, activeAudioSources);

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            // islandRoom.LocalTrackPublished += OnLocalTrackPublished;
            // islandRoom.LocalTrackUnpublished += OnLocalTrackUnpublished;

            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
            proximityStateSubscription = stateModel.State.Subscribe(OnProximityStateChanged);

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Initialized, waiting for Island Room connection");

            if (islandRoom.Info.ConnectionState == ConnectionState.ConnConnected
                && stateModel.State.Value is ProximityVoiceChatState.HEARING or ProximityVoiceChatState.SPEAKING)
                ActivateWithRetryAsync(activationCts.Token).Forget();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            activationCts.SafeCancelAndDispose();
            callStatusSubscription.Dispose();
            proximityStateSubscription?.Dispose();

            islandRoom.ConnectionUpdated -= OnConnectionUpdated;
            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
            // islandRoom.LocalTrackPublished -= OnLocalTrackPublished;
            // islandRoom.LocalTrackUnpublished -= OnLocalTrackUnpublished;

            Deactivate();
            micPublisher.Dispose();
            remoteListener.Dispose();

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Disposed");
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsInCall())
                stateModel.Suppress();
            else if (status.IsNotConnected())
                stateModel.Resume();
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackSubscribed(publication, participant).Forget();

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackUnsubscribed(publication, participant).Forget();

        // private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
        //     => remoteListener.HandleTrackSubscribed(publication, participant, isLocalLoopback: true);
        //
        // private void OnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        //     => remoteListener.HandleTrackUnsubscribed(publication, participant, isLocalLoopback: true);

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate update, DisconnectReason? reason)
        {
            OnConnectionUpdatedInternalAsync(update, reason).Forget();
            return;

            async UniTaskVoid OnConnectionUpdatedInternalAsync(ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason)
            {
                await UniTask.SwitchToMainThread();

                ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT,
                    $"Island Room connection: {connectionUpdate}{(disconnectReason.HasValue ? $" (reason: {disconnectReason.Value})" : "")}");

                switch (connectionUpdate)
                {
                    case ConnectionUpdate.Connected:
                        if (stateModel.State.Value is ProximityVoiceChatState.HEARING or ProximityVoiceChatState.SPEAKING)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }
                        break;

                    case ConnectionUpdate.Disconnected:
                        activationCts.SafeCancelAndDispose();

                        if (VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(disconnectReason))
                            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"Valid disconnect ({disconnectReason}) — no reconnection needed");

                        Deactivate();
                        break;
                }
            }
        }

        private async UniTask ActivateWithRetryAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            for (var attempt = 1; attempt <= configuration.MaxReconnectionAttempts; attempt++)
            {
                if (ct.IsCancellationRequested || disposed) return;

                try
                {
                    await micPublisher.PublishAsync(false, ct);
                    remoteListener.StartListening().Forget();

                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Activated — publishing and listening with 3D spatial audio");

                    if (stateModel.State.Value == ProximityVoiceChatState.SPEAKING)
                        microphoneHandler.EnableMicrophone();

                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT,
                        $"Activation attempt {attempt}/{configuration.MaxReconnectionAttempts} failed: {ex.Message}");

                    Deactivate();

                    if (attempt >= configuration.MaxReconnectionAttempts)
                    {
                        ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, "All activation attempts exhausted");
                        return;
                    }

                    try { await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }

        private void OnProximityStateChanged(ProximityVoiceChatState newState)
        {
            OnProximityStateChangedInternalAsync(newState).Forget();
            return;

            async UniTaskVoid OnProximityStateChangedInternalAsync(ProximityVoiceChatState state)
            {
                await UniTask.SwitchToMainThread();

                if (disposed) return;

                switch (state)
                {
                    case ProximityVoiceChatState.DISABLED:
                        activationCts.SafeCancelAndDispose();
                        Deactivate();
                        break;

                    case ProximityVoiceChatState.HEARING:
                    case ProximityVoiceChatState.SPEAKING:
                        if (remoteListener.IsSuppressed)
                        {
                            remoteListener.UnmuteAll();
                            microphoneHandler.Assign(micPublisher.CurrentMicrophone, VoiceChatType.PROXIMITY);
                            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Resumed — no active call");
                        }
                        else if (!micPublisher.isPublished && islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }
                        break;

                    case ProximityVoiceChatState.SUPPRESSED:
                        microphoneHandler.ClearSource(VoiceChatType.PROXIMITY);
                        remoteListener.MuteAll();
                        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Suppressed — Private/Community call active");
                        break;
                }
            }
        }

        private void Deactivate()
        {
            micPublisher.Unpublish();
            remoteListener.StopListening().Forget();
            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Deactivated");
        }
    }
}
