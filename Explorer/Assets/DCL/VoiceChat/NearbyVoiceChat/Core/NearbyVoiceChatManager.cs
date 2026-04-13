using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Orchestrates nearby voice chat: coordinates state transitions (Hearing/Speaking/Suppressed/Disabled),
    ///     delegates microphone publishing to <see cref="MicrophoneTrackPublisher"/>,
    ///     delegates remote track management to <see cref="RemoteTrackListener"/>.
    ///     Mic control (PTT, device switching) is handled by <see cref="VoiceChatMicrophoneHandler"/>.
    /// </summary>
    public class NearbyVoiceChatManager : IDisposable
    {
        private const string TAG = nameof(NearbyVoiceChatManager);

        private readonly VoiceChatConfiguration configuration;
        private readonly IRoom islandRoom;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly MicrophoneTrackPublisher micPublisher;
        private readonly RemoteTrackListener remoteListener;

        private readonly IDisposable callStatusSubscription;
        private readonly IDisposable? nearbyStateSubscription;

        private CancellationTokenSource activationCts = new ();

        private bool disposed;

        public NearbyVoiceChatManager(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus,
            NearbyVoiceChatStateModel stateModel,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.stateModel = stateModel;
            this.microphoneHandler = microphoneHandler;

            micPublisher = new MicrophoneTrackPublisher(islandRoom, configuration, microphoneHandler, VoiceChatType.NEARBY);

            var nearbyHub = new PlaybackSourcesHub(
                parentNameSuffix: "Nearby",
                configuration.NearbyChatAudioMixerGroup,
                spatial: true,
                onSourceConfigured: (key, lkSource) =>
                {
                    configuration.ApplyNearbySettings(lkSource.AudioSource);
                    configuration.ApplyLivekitSpatialSettings(lkSource);
                    activeAudioSources[key.identity] = lkSource;
                },
                onSourceRemoved: key => activeAudioSources.TryRemove(key.identity, out _));

            remoteListener = new RemoteTrackListener(islandRoom, configuration, nearbyHub);

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;

            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
            nearbyStateSubscription = stateModel.State.Subscribe(OnNearbyStateChanged);

            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Initialized, waiting for Island Room connection");

            if (islandRoom.Info.ConnectionState == ConnectionState.ConnConnected
                && stateModel.State.Value is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING)
                ActivateWithRetryAsync(activationCts.Token).Forget();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            activationCts.SafeCancelAndDispose();
            callStatusSubscription.Dispose();
            nearbyStateSubscription?.Dispose();

            islandRoom.ConnectionUpdated -= OnConnectionUpdated;
            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;

            Deactivate();
            micPublisher.Dispose();
            remoteListener.Dispose();

            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"{TAG} Disposed");
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsInCall())
                stateModel.Suppress();
            else if (status.IsNotConnected())
                stateModel.Resume();
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackSubscribedAsync(publication, participant).Forget();

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackUnsubscribedAsync(publication, participant).Forget();

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate update, DisconnectReason? reason)
        {
            OnConnectionUpdatedInternalAsync(update, reason).Forget();
            return;

            async UniTaskVoid OnConnectionUpdatedInternalAsync(ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason)
            {
                await UniTask.SwitchToMainThread();

                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT,
                    $"Island Room connection: {connectionUpdate}{(disconnectReason.HasValue ? $" (reason: {disconnectReason.Value})" : "")}");

                switch (connectionUpdate)
                {
                    case ConnectionUpdate.Connected:
                        if (stateModel.State.Value is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }
                        break;

                    case ConnectionUpdate.Disconnected:
                        activationCts.SafeCancelAndDispose();

                        if (VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(disconnectReason))
                            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"Valid disconnect ({disconnectReason}) — no reconnection needed");

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
                    remoteListener.StartListeningAsync().Forget();

                    ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Activated — publishing and listening with 3D spatial audio");

                    if (stateModel.State.Value == NearbyVoiceChatState.SPEAKING)
                        microphoneHandler.EnableMicrophone();

                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.NEARBY_VOICE_CHAT,
                        $"Activation attempt {attempt}/{configuration.MaxReconnectionAttempts} failed: {ex.Message}");

                    Deactivate();

                    if (attempt >= configuration.MaxReconnectionAttempts)
                    {
                        ReportHub.LogError(ReportCategory.NEARBY_VOICE_CHAT, "All activation attempts exhausted");
                        return;
                    }

                    try { await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }

        private void OnNearbyStateChanged(NearbyVoiceChatState newState)
        {
            OnNearbyStateChangedInternalAsync(newState).Forget();
            return;

            async UniTaskVoid OnNearbyStateChangedInternalAsync(NearbyVoiceChatState state)
            {
                await UniTask.SwitchToMainThread();

                if (disposed) return;

                switch (state)
                {
                    case NearbyVoiceChatState.DISABLED:
                    case NearbyVoiceChatState.SUPPRESSED:
                        activationCts.SafeCancelAndDispose();
                        Deactivate();
                        break;

                    case NearbyVoiceChatState.IDLE:
                    case NearbyVoiceChatState.SPEAKING:
                        if (!micPublisher.isPublished && islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }
                        break;
                }
            }
        }

        private void Deactivate()
        {
            micPublisher.Unpublish();
            remoteListener.StopListeningAsync().Forget();
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Deactivated");
        }
    }
}
