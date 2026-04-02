using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.VoiceChat.Proximity.UI;
#if UNITY_STANDALONE_OSX
using DCL.VoiceChat.Permissions;
#endif
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Manages proximity voice chat with 3D spatial audio by publishing and subscribing
    /// to audio tracks in the Island Room. Registers active audio sources in a shared dictionary
    /// so <see cref="Systems.ProximityAudioPositionSystem"/> can assign and sync positions via ECS.
    /// </summary>
    public class ProximityVoiceChatManager : IDisposable
    {
        private const string TAG = nameof(ProximityVoiceChatManager);

        private readonly VoiceChatConfiguration configuration;

        private readonly IRoom islandRoom;

        private readonly ConcurrentDictionary<StreamKey, LivekitAudioSource> remoteSources = new ();
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly Transform fallbackParent;

        private readonly ProximityVoiceChatStateModel stateModel;

        private readonly IDisposable callStatusSubscription;
        private readonly IDisposable? proximityStateSubscription;

        private MicrophoneRtcAudioSource? rtcAudioSource;
        private ITrack? localTrack;
        private LivekitAudioSource? loopbackSource;

        private CancellationTokenSource activationCts = new ();

        private bool published;
        private bool disposed;
        private bool suppressed;

        public ProximityVoiceChatManager(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            ConcurrentDictionary<string, AudioSource> activeAudioSources,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus,
            ProximityVoiceChatStateModel stateModel)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.activeAudioSources = activeAudioSources;
            this.stateModel = stateModel;

            fallbackParent = new GameObject($"{nameof(ProximityVoiceChatManager)}_FallbackParent").transform;

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

            if (fallbackParent != null)
                fallbackParent.gameObject.SelfDestroy();

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Disposed");
        }

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

        private async UniTask ActivateWithRetryAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            for (var attempt = 1; attempt <= configuration.MaxReconnectionAttempts; attempt++)
            {
                if (ct.IsCancellationRequested || disposed) return;

                try
                {
                    await PublishLocalTrackAsync(ct);
                    SubscribeToExistingRemoteTracks();

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

        private void SubscribeToExistingRemoteTracks()
        {
            foreach (KeyValuePair<string, Participant> entry in islandRoom.Participants.RemoteParticipantIdentities())
            foreach ((string sid, TrackPublication pub) in entry.Value.Tracks)
            {
                if (pub.Kind != TrackKind.KindAudio) continue;

                var key = new StreamKey(entry.Key!, sid);
                Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);

                if (stream.Resource.Has)
                    AddRemoteSource(key, stream);
            }
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            OnTrackSubscribedInternalAsync().Forget();
            return;

            async UniTaskVoid OnTrackSubscribedInternalAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;

                try
                {
                    var key = new StreamKey(participant.Identity, publication.Sid);
                    Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);

                    if (stream.Resource.Has)
                        AddRemoteSource(key, stream);
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle track subscription: {ex.Message}");
                }
            }
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            OnTrackUnsubscribedInternalAsync().Forget();
            return;

            async UniTaskVoid OnTrackUnsubscribedInternalAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;

                try
                {
                    RemoveRemoteSource(new StreamKey(participant.Identity, publication.Sid));
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle track unsubscription: {ex.Message}");
                }
            }
        }

        private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            OnLocalTrackPublishedInternalAsync().Forget();
            return;

            async UniTaskVoid OnLocalTrackPublishedInternalAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;
                if (!configuration.EnableLocalTrackPlayback) return;

                try
                {
                    var key = new StreamKey(participant.Identity, publication.Sid);
                    Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);

                    if (stream.Resource.Has)
                    {
                        loopbackSource = CreateSource(key, stream, spatial: false);
                        loopbackSource.transform.SetParent(fallbackParent);
                        loopbackSource.Play();
                        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Local track loopback enabled (round-trip via server)");
                    }
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle local track published: {ex.Message}");
                }
            }
        }

        private void OnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            OnLocalTrackUnpublishedInternalAsync().Forget();
            return;

            async UniTaskVoid OnLocalTrackUnpublishedInternalAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;
                if (!configuration.EnableLocalTrackPlayback) return;

                try
                {
                    DestroySource(loopbackSource);
                    loopbackSource = null;
                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Local track loopback removed");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle local track unpublished: {ex.Message}");
                }
            }
        }

        private void AddRemoteSource(StreamKey key, Weak<AudioStream> stream)
        {
            if (remoteSources.TryRemove(key, out LivekitAudioSource? oldSource))
                DestroySource(oldSource);

            LivekitAudioSource source = CreateSource(key, stream, spatial: true);
            source.transform.SetParent(fallbackParent);
            source.Play();

            if (!remoteSources.TryAdd(key, source))
            {
                ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, $"Cannot add proximity source, key already exists: {key}");
                DestroySource(source);
                return;
            }

            AudioSource audioSource = source.GetComponent<AudioSource>();
            activeAudioSources[key.identity] = audioSource;

            if (audioSource != null && suppressed)
                audioSource.mute = true;

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"3D audio source added for {key.identity}{(suppressed ? " (muted — call active)" : "")}");
        }

        private void RemoveRemoteSource(StreamKey key)
        {
            if (!remoteSources.TryRemove(key, out LivekitAudioSource? source))
                return;

            activeAudioSources.TryRemove(key.identity, out _);
            DestroySource(source);
            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"Remote source removed for {key.identity}");
        }

        private LivekitAudioSource CreateSource(StreamKey key, Weak<AudioStream> stream, bool spatial)
        {
            LivekitAudioSource source = LivekitAudioSource.New(explicitName: true, spatial: spatial);

            AudioSource audioSource = source.GetComponent<AudioSource>().EnsureNotNull();
            audioSource.outputAudioMixerGroup = configuration.ProximityChatAudioMixerGroup;

            if (spatial)
            {
                configuration.ApplyProximitySettingsTo(audioSource);
                configuration.ApplySpatializationSettingsTo(source);
                source.gameObject.AddComponent<ProximityPanCalculator>();
            }

            source.Construct(stream);
            source.name = $"ProximityAudio_{key.identity}";
            return source;
        }

        private static void DestroySource(LivekitAudioSource? source)
        {
            if (source == null) return;

            source.Stop();
            source.Free();
            source.gameObject.SelfDestroy();
        }

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
                        if (suppressed)
                            ResumeProximity();
                        else if (!published && islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }

                        rtcAudioSource?.Stop();
                        break;

                    case ProximityVoiceChatState.SPEAKING:
                        if (suppressed)
                            ResumeProximity();

                        if (!published && islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                        {
                            activationCts = activationCts.SafeRestart();
                            await ActivateWithRetryAsync(activationCts.Token);
                        }

                        rtcAudioSource?.Start();
                        break;

                    case ProximityVoiceChatState.SUPPRESSED:
                        SuppressProximity();
                        break;
                }
            }
        }

        private void SuppressProximity()
        {
            if (suppressed) return;
            suppressed = true;

            rtcAudioSource?.Stop();

            foreach (LivekitAudioSource source in remoteSources.Values)
            {
                AudioSource audioSource = source.GetComponent<AudioSource>();
                if (audioSource != null) audioSource.mute = true;
            }

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Suppressed — Private/Community call active");
        }

        private void ResumeProximity()
        {
            if (!suppressed) return;
            suppressed = false;

            foreach (KeyValuePair<StreamKey, LivekitAudioSource> entry in remoteSources)
            {
                AudioSource audioSource = entry.Value.GetComponent<AudioSource>();

                if (audioSource != null)
                    audioSource.mute = false;
            }

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Resumed — no active call");
        }

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

        private void Deactivate()
        {
            UnpublishLocalTrack();

            DestroySource(loopbackSource);
            loopbackSource = null;

            foreach (StreamKey key in remoteSources.Keys)
                RemoveRemoteSource(key);

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Deactivated");
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
    }
}
