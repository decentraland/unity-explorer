using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.LiveKit.Public;
using DCL.RealmNavigation;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Runtime.Scripts.Audio;
using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Orchestrates nearby voice chat: coordinates state transitions (Hearing/Speaking/Suppressed/Disabled),
    ///     delegates microphone publishing to <see cref="MicrophoneTrackPublisher"/>,
    ///     delegates remote track management to <see cref="RemoteTrackListener"/>.
    ///     Owns nearby microphone lifecycle: start/stop, focus handling, device switching.
    /// </summary>
    public class NearbyVoiceChatManager : IDisposable
    {
        private const string TAG = nameof(NearbyVoiceChatManager);

        private readonly VoiceChatConfiguration configuration;
        private readonly IRoom islandRoom;

        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyMuteService muteService;
        private readonly IUserBlockingCache userBlockingCache;

        private readonly MicrophoneTrackPublisher micPublisher;
        private readonly RemoteTrackListener remoteListener;

        private readonly IDisposable callStatusSubscription;
        private readonly IDisposable loadingStageSubscription;
        private readonly IDisposable? nearbyStateSubscription;

        private CancellationTokenSource? activationCts;
        private bool wasNearbyMicActiveBeforeFocusLoss;

        private bool disposed;

        public NearbyVoiceChatManager(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus,
            NearbyVoiceChatStateModel stateModel,
            NearbyMuteService muteService,
            IUserBlockingCache userBlockingCache,
            ILoadingStatus loadingStatus)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.stateModel = stateModel;
            this.muteService = muteService;
            this.userBlockingCache = userBlockingCache;

            micPublisher = new MicrophoneTrackPublisher(islandRoom, configuration, VoiceChatType.NEARBY);

            var nearbyHub = new PlaybackSourcesHub(
                parentNameSuffix: "Nearby",
                configuration.ChatAudioMixerGroup,
                spatial: true,
                onSourceConfigured: (key, lkSource) =>
                {
                    lkSource.AudioSource.Apply3dAudioSettings(configuration.NearbyCustomRolloffCurve);
                    lkSource.ApplySpatialSettings(configuration);
                    activeAudioSources[key.identity] = lkSource;

                    lkSource.AudioSource.mute = muteService.IsMuted(key.identity);
                },
                onSourceRemoved: key => activeAudioSources.TryRemove(key.identity, out _));

            remoteListener = new RemoteTrackListener(islandRoom, configuration, nearbyHub, userBlockingCache);

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;

            Application.focusChanged += OnApplicationFocusChanged;
            VoiceChatSettings.MicrophoneChanged += OnMicrophoneDeviceChanged;

            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
            nearbyStateSubscription = stateModel.State.Subscribe(OnNearbyStateChanged);

            muteService.MuteStateChanged += OnMuteStateChanged;

            userBlockingCache.UserBlocked += OnUserBlocked;
            userBlockingCache.UserBlocksYou += OnUserBlocked;
            userBlockingCache.UserUnblocked += OnUserUnblocked;
            userBlockingCache.UserUnblocksYou += OnUserUnblocked;

            // Suppress while world is still loading so we do not attempt to connect before the player spawns.
            // User preference (DISABLED/IDLE from PlayerPrefs) is preserved as preBlockedState and restored on Resume(LOADING).
            if (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                stateModel.Suppress(SuppressionReason.LOADING);

            loadingStageSubscription = loadingStatus.CurrentStage.Subscribe(OnLoadingStageChanged);

            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Initialized, waiting for Island Room connection");
            Connect();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            activationCts.SafeCancelAndDispose();
            callStatusSubscription.Dispose();
            loadingStageSubscription.Dispose();
            nearbyStateSubscription?.Dispose();

            muteService.MuteStateChanged -= OnMuteStateChanged;

            userBlockingCache.UserBlocked -= OnUserBlocked;
            userBlockingCache.UserBlocksYou -= OnUserBlocked;
            userBlockingCache.UserUnblocked -= OnUserUnblocked;
            userBlockingCache.UserUnblocksYou -= OnUserUnblocked;

            islandRoom.ConnectionUpdated -= OnConnectionUpdated;
            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;

            Application.focusChanged -= OnApplicationFocusChanged;
            VoiceChatSettings.MicrophoneChanged -= OnMicrophoneDeviceChanged;

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
            PublishMicWithRetryAsync(startMic: stateModel.State.Value == NearbyVoiceChatState.SPEAKING).Forget();
        }

        private void Disconnect()
        {
            stateModel.IsLocalSpeaking = false;
            micPublisher.Unpublish();
            remoteListener.StopListeningAsync().Forget();
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Deactivated");
        }

        private void OnActiveSpeakersUpdated()
        {
            LKParticipant local = islandRoom.Participants.LocalParticipant();
            if (local != null)
                stateModel.IsLocalSpeaking = islandRoom.ActiveSpeakers.Contains(local.Identity);
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsInCall())
                stateModel.Suppress(SuppressionReason.CALL);
            else if (status.IsNotConnected())
                stateModel.Resume(SuppressionReason.CALL);
        }

        private void OnLoadingStageChanged(LoadingStatus.LoadingStage stage)
        {
            if (stage == LoadingStatus.LoadingStage.Completed)
                stateModel.Resume(SuppressionReason.LOADING);
            else
                stateModel.Suppress(SuppressionReason.LOADING);
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (stateModel.State.Value is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING)
                remoteListener.HandleTrackSubscribedAsync(publication, participant).Forget();
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, LKParticipant participant) =>
            remoteListener.HandleTrackUnsubscribedAsync(publication, participant).Forget();

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!hasFocus && stateModel.State.Value == NearbyVoiceChatState.SPEAKING && micPublisher.isRecording)
            {
                micPublisher.StopMicrophone();
                wasNearbyMicActiveBeforeFocusLoss = true;
                stateModel.StopSpeaking();
                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Nearby mic paused — application lost focus");
            }
            else if (hasFocus && wasNearbyMicActiveBeforeFocusLoss)
            {
                wasNearbyMicActiveBeforeFocusLoss = false;

                if (stateModel.State.Value == NearbyVoiceChatState.SUPPRESSED)
                {
                    ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Nearby mic NOT resumed — state is SUPPRESSED");
                    return;
                }

                stateModel.StartSpeaking(NearbyVoiceActivation.FOCUS_RESUMED);
                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Nearby mic resumed — application regained focus");
            }
        }

        private void OnMicrophoneDeviceChanged(MicrophoneSelection selection)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                SwitchAsync().Forget();
                return;
            }

            RepublishIfNeeded();
            return;

            async UniTaskVoid SwitchAsync()
            {
                await UniTask.SwitchToMainThread();
                RepublishIfNeeded();
            }

            void RepublishIfNeeded()
            {
                if (!micPublisher.isPublished) return;

                bool wasSpeaking = stateModel.State.Value == NearbyVoiceChatState.SPEAKING;
                micPublisher.Unpublish();
                PublishMicWithRetryAsync(startMic: wasSpeaking).Forget();
            }
        }

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

        private async UniTask PublishMicWithRetryAsync(bool startMic = false)
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

                    if (ct.IsCancellationRequested || disposed) return;

                    if (startMic && stateModel.State.Value == NearbyVoiceChatState.SPEAKING)
                        micPublisher.StartMicrophone();

                    ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, startMic ? "Mic track published and started" : "Mic track published (standby)");
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

        private void OnMuteStateChanged(string walletId, bool isMuted) =>
            remoteListener.SetMuteForIdentity(walletId, isMuted);

        private void OnUserBlocked(string userId)
        {
            if (disposed) return;

            remoteListener.RemoveStreamsByIdentity(userId);
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"Removed nearby audio for blocked user {userId}");
        }

        private void OnUserUnblocked(string userId)
        {
            if (disposed) return;

            // Only re-add while listening — suppressed/disabled states will rehydrate via StartListeningAsync on resume.
            if (stateModel.State.Value is not (NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING))
                return;

            remoteListener.AddStreamsForIdentityAsync(userId).Forget();
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"Restoring nearby audio for unblocked user {userId}");
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
                        if (micPublisher.isRecording)
                            micPublisher.StopMicrophone();

                        Connect(); // ensures listener + track published; no-ops if already done
                        break;

                    case NearbyVoiceChatState.SPEAKING:
                        if (micPublisher.isPublished)
                            micPublisher.StartMicrophone();
                        else
                            Connect(); // publishes + starts mic
                        break;
                }
            }
        }
    }
}
