using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.VoiceChat.Proximity.UI;
#if UNITY_STANDALONE_OSX
using DCL.VoiceChat.Permissions;
#endif
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Orchestrates proximity voice chat: publishes/unpublishes the local microphone track,
    /// coordinates state transitions (Hearing/Speaking/Suppressed/Disabled), and delegates
    /// remote track management to <see cref="ProximityRemoteTrackListener"/>.
    /// </summary>
    public class ProximityVoiceChatManager : IDisposable
    {
        private const string TAG = nameof(ProximityVoiceChatManager);

        private readonly VoiceChatConfiguration configuration;
        private readonly IRoom islandRoom;
        private readonly ProximityVoiceChatStateModel stateModel;
        private readonly ProximityRemoteTrackListener remoteListener;

        private readonly IDisposable callStatusSubscription;
        private readonly IDisposable? proximityStateSubscription;

        private MicrophoneRtcAudioSource? rtcAudioSource;
        private ITrack? localTrack;

        private CancellationTokenSource activationCts = new ();

        private bool published;
        private bool disposed;

        public ProximityVoiceChatManager(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            ConcurrentDictionary<string, AudioSource> activeAudioSources,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus,
            ProximityVoiceChatStateModel stateModel)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.stateModel = stateModel;

            remoteListener = new ProximityRemoteTrackListener(islandRoom, configuration, activeAudioSources);

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            islandRoom.LocalTrackPublished += OnLocalTrackPublished;
            islandRoom.LocalTrackUnpublished += OnLocalTrackUnpublished;

            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
            proximityStateSubscription = stateModel.State.Subscribe(OnProximityStateChanged);
            VoiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;

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
            VoiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;

            islandRoom.ConnectionUpdated -= OnConnectionUpdated;
            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
            islandRoom.LocalTrackPublished -= OnLocalTrackPublished;
            islandRoom.LocalTrackUnpublished -= OnLocalTrackUnpublished;

            Deactivate();
            remoteListener.Dispose();

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Disposed");
        }

        // --- Room event delegates ---

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackSubscribed(publication, participant);

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackUnsubscribed(publication, participant);

        private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackSubscribed(publication, participant, isLocalLoopback: true);

        private void OnLocalTrackUnpublished(TrackPublication publication, Participant participant)
            => remoteListener.HandleTrackUnsubscribed(publication, participant, isLocalLoopback: true);

        // --- Connection ---

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
                        Deactivate();
                        break;
                }
            }
        }

        // --- Activation ---

        private async UniTask ActivateWithRetryAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            for (var attempt = 1; attempt <= configuration.MaxReconnectionAttempts; attempt++)
            {
                if (ct.IsCancellationRequested || disposed) return;

                try
                {
                    await PublishLocalTrackAsync(ct);
                    remoteListener.StartListening();

                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Activated — publishing and listening with 3D spatial audio");

                    if (stateModel.State.Value != ProximityVoiceChatState.SPEAKING)
                        rtcAudioSource?.Stop();

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

        // --- Local track ---

        private async UniTask PublishLocalTrackAsync(CancellationToken ct)
        {
            if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
                configuration.AudioMixerGroup.audioMixer.SetFloat(nameof(AudioMixerExposedParam.Microphone_Volume), 13);

#if UNITY_STANDALONE_OSX
            bool hasPermissions = await VoiceChatPermissions.GuardAsync(ct);

            if (!hasPermissions)
            {
                ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, "Microphone permissions not granted, cannot publish local track");
                return;
            }
#endif

            Result<MicrophoneSelection> reachable = VoiceChatSettings.ReachableSelection();

            if (!reachable.Success)
                throw new InvalidOperationException($"No microphone available: {reachable.ErrorMessage}");

            Result<MicrophoneRtcAudioSource> result = MicrophoneRtcAudioSource.New(
                reachable.Value,
                (configuration.AudioMixerGroup.audioMixer, nameof(AudioMixerExposedParam.Microphone_Volume)),
                configuration.microphonePlaybackToSpeakers
            );

            if (!result.Success)
                throw new InvalidOperationException($"Failed to create RTC audio source: {result.ErrorMessage}");

            rtcAudioSource = result.Value;
            rtcAudioSource.Start();

            string participantName = islandRoom.Participants.LocalParticipant().Name;

            localTrack = islandRoom.LocalTracks.CreateAudioTrack(
                $"proximity_{participantName}",
                rtcAudioSource
            );

            var options = new TrackPublishOptions
            {
                AudioEncoding = new AudioEncoding { MaxBitrate = 124000 },
                Source = TrackSource.SourceMicrophone,
            };

            islandRoom.Participants.LocalParticipant().PublishTrack(localTrack, options, ct);
            published = true;

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Local track published to Island Room");
        }

        private void UnpublishLocalTrack()
        {
            if (localTrack != null && published)
            {
                try
                {
                    islandRoom.Participants.LocalParticipant().UnpublishTrack(localTrack, true);
                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Local track unpublished");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Error unpublishing: {ex.Message}");
                }
            }

            rtcAudioSource?.Dispose();
            rtcAudioSource = null;
            localTrack = null;
            published = false;
        }

        // --- State ---

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsInCall())
                stateModel.Suppress();
            else if (status.IsNotConnected())
                stateModel.Resume();
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
                        if (remoteListener.isSuppressed)
                        {
                            remoteListener.UnmuteAll();
                            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Resumed — no active call");
                        }
                        else if (!published && islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }

                        rtcAudioSource?.Stop();
                        break;

                    case ProximityVoiceChatState.SPEAKING:
                        if (remoteListener.isSuppressed)
                        {
                            remoteListener.UnmuteAll();
                            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Resumed — no active call");
                        }
                        else if (!published && islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }

                        rtcAudioSource?.Start();
                        break;

                    case ProximityVoiceChatState.SUPPRESSED:
                        rtcAudioSource?.Stop();
                        remoteListener.MuteAll();
                        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Suppressed — Private/Community call active");
                        break;
                }
            }
        }

        // --- Microphone ---

        private void OnMicrophoneChanged(MicrophoneSelection newSelection)
        {
            if (rtcAudioSource == null) return;

            SwitchMicrophoneAsync(newSelection).Forget();
            return;

            async UniTaskVoid SwitchMicrophoneAsync(MicrophoneSelection selection)
            {
                if (!PlayerLoopHelper.IsMainThread)
                    await UniTask.SwitchToMainThread();

                SwitchMicrophoneInternal(selection);
            }
        }

        private void SwitchMicrophoneInternal(MicrophoneSelection selection)
        {
            Result result = rtcAudioSource!.SwitchMicrophone(selection);

            if (result.Success)
                ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"Microphone switched to: {selection.name}");
            else
                ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to switch microphone: {result.ErrorMessage}");
        }

        // --- Deactivation ---

        private void Deactivate()
        {
            UnpublishLocalTrack();
            remoteListener.StopListening();
            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Deactivated");
        }
    }
}
