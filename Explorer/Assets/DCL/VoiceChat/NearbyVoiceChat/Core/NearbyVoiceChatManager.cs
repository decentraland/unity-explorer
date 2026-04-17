using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using System;
using System.Collections.Concurrent;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
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

        private CancellationTokenSource? activationCts;

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
                    lkSource.AudioSource.Apply3dAudioSettings(configuration.NearbyCustomRolloffCurve);
                    lkSource.ApplySpatialSettings(configuration);
                    activeAudioSources[key.identity] = lkSource;
                },
                onSourceRemoved: key => activeAudioSources.TryRemove(key.identity, out _));

            remoteListener = new RemoteTrackListener(islandRoom, configuration, nearbyHub);

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;

            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
            nearbyStateSubscription = stateModel.State.Subscribe(OnNearbyStateChanged);

            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Initialized, waiting for Island Room connection");
            Connect();
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
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;

            Disconnect();
            micPublisher.Dispose();
            remoteListener.Dispose();

            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"{TAG} Disposed");
        }

        private void Connect()
        {
            if (islandRoom.Info.ConnectionState != LKConnectionState.ConnConnected) return;
            if (stateModel.State.Value is not (NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING)) return;

            remoteListener.StartListeningAsync().Forget();

            if (stateModel.State.Value == NearbyVoiceChatState.SPEAKING)
                PublishMicWithRetryAsync().Forget();
        }

        private void Disconnect()
        {
            stateModel.IsLocalSpeaking = false;
            micPublisher.Unpublish();
            remoteListener.StopListeningAsync().Forget();
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Deactivated");
        }

        private void OnActiveSpeakersUpdated() =>
            stateModel.IsLocalSpeaking =
                islandRoom.ActiveSpeakers.Contains(
                    islandRoom.Participants.LocalParticipant().Identity);

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsInCall())
                stateModel.Suppress(SuppressionReason.CALL);
            else if (status.IsNotConnected())
                stateModel.Resume(SuppressionReason.CALL);
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (stateModel.State.Value is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING)
                remoteListener.HandleTrackSubscribedAsync(publication, participant).Forget();
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, LKParticipant participant) =>
            remoteListener.HandleTrackUnsubscribedAsync(publication, participant).Forget();

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate update, LKDisconnectReason? reason)
        {
            // Cancel in-flight mic publish immediately so PublishAsync observes it via its CancellationToken
            if (update == ConnectionUpdate.Disconnected)
                activationCts.SafeCancelAndDispose();

            OnConnectionUpdatedInternalAsync(update, reason).Forget();
            return;

            async UniTaskVoid OnConnectionUpdatedInternalAsync(ConnectionUpdate connectionUpdate, LKDisconnectReason? disconnectReason)
            {
                if (!PlayerLoopHelper.IsMainThread)
                    await UniTask.SwitchToMainThread();

                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT,
                    $"Island Room connection: {connectionUpdate}{(disconnectReason.HasValue ? $" (reason: {disconnectReason.Value})" : "")}");

                if (connectionUpdate == ConnectionUpdate.Disconnected)
                {
                    if (VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(disconnectReason))
                        ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"Valid disconnect ({disconnectReason}) — no reconnection needed");

                    Disconnect();
                }
                else
                    Connect();
            }
        }

        private async UniTask PublishMicWithRetryAsync()
        {
            activationCts = activationCts.SafeRestart();
            var ct = activationCts.Token;

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread(ct);

            for (var attempt = 1; attempt <= configuration.MaxReconnectionAttempts; attempt++)
            {
                if (ct.IsCancellationRequested || disposed) return;

                try
                {
                    await micPublisher.PublishAsync(false, ct);
                    microphoneHandler.EnableMicrophone();

                    ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Mic track published and enabled");
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.NEARBY_VOICE_CHAT,
                        $"Mic publish attempt {attempt}/{configuration.MaxReconnectionAttempts} failed: {ex.Message}");

                    micPublisher.Unpublish();

                    if (attempt >= configuration.MaxReconnectionAttempts)
                    {
                        ReportHub.LogWarning(ReportCategory.NEARBY_VOICE_CHAT, "All mic publish attempts exhausted — listening-only mode active");
                        return;
                    }

                    try { await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }

        private void OnNearbyStateChanged(NearbyVoiceChatState newState)
        {
            // Cancel in-flight mic publish immediately so PublishAsync observes it via its CancellationToken
            if (newState is NearbyVoiceChatState.SUPPRESSED or NearbyVoiceChatState.DISABLED)
                activationCts.SafeCancelAndDispose();

            OnNearbyStateChangedInternalAsync(newState).Forget();
            return;

            async UniTaskVoid OnNearbyStateChangedInternalAsync(NearbyVoiceChatState state)
            {
                if (!PlayerLoopHelper.IsMainThread)
                    await UniTask.SwitchToMainThread();

                if (disposed) return;

                switch (state)
                {
                    case NearbyVoiceChatState.DISABLED:
                    case NearbyVoiceChatState.SUPPRESSED:
                        Disconnect();
                        break;

                    case NearbyVoiceChatState.IDLE:
                        if (micPublisher.isPublished)
                            microphoneHandler.DisableMicrophone();

                        if (islandRoom.Info.ConnectionState == LKConnectionState.ConnConnected)
                            remoteListener.StartListeningAsync().Forget();

                        break;

                    case NearbyVoiceChatState.SPEAKING:
                        if (micPublisher.isPublished) // already connected
                            microphoneHandler.EnableMicrophone();
                        else
                            Connect();

                        break;
                }
            }
        }
    }
}
