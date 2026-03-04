using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.Settings.Settings;
using DCL.Utilities.Extensions;
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
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Manages proximity voice chat by publishing and subscribing to audio tracks
    /// in the Island Room. Auto-activates when Island Room connects.
    /// </summary>
    public class ProximityVoiceChatManager : IDisposable
    {
        private const string TAG = nameof(ProximityVoiceChatManager);

        private readonly IRoom islandRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly PlaybackSourcesHub playbackSourcesHub;

        private MicrophoneRtcAudioSource? rtcAudioSource;
        private ITrack? localTrack;
        private bool published;
        private bool disposed;

        public ProximityVoiceChatManager(IRoom islandRoom, VoiceChatConfiguration configuration)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;

            playbackSourcesHub = new PlaybackSourcesHub(configuration.ChatAudioMixerGroup.EnsureNotNull());

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            islandRoom.LocalTrackPublished += OnLocalTrackPublished;
            islandRoom.LocalTrackUnpublished += OnLocalTrackUnpublished;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Initialized, waiting for Island Room connection");

            if (islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                ActivateAsync(CancellationToken.None).Forget();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            islandRoom.ConnectionUpdated -= OnConnectionUpdated;
            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
            islandRoom.LocalTrackPublished -= OnLocalTrackPublished;
            islandRoom.LocalTrackUnpublished -= OnLocalTrackUnpublished;

            Deactivate();
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate update, DisconnectReason? reason)
        {
            SwitchActivationAsync(update).Forget();
            return;

            async UniTaskVoid SwitchActivationAsync(ConnectionUpdate connectionUpdate)
            {
                await UniTask.SwitchToMainThread();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Island Room connection: {connectionUpdate}");

                switch (connectionUpdate)
                {
                    case ConnectionUpdate.Connected:
                        if (!published)
                            await ActivateAsync(CancellationToken.None);
                        break;

                    case ConnectionUpdate.Disconnected:
                        Deactivate();
                        break;
                }
            }
        }

        private async UniTask ActivateAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            try
            {
                PublishLocalTrack(ct);
                SubscribeToExistingRemoteTracks();
                playbackSourcesHub.Play();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Activated — publishing and listening");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Activation failed: {ex.Message}");
                Deactivate();
            }
        }

        private void PublishLocalTrack(CancellationToken ct)
        {
            if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
                configuration.AudioMixerGroup.audioMixer.SetFloat(nameof(AudioMixerExposedParam.Microphone_Volume), 13);

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

            localTrack = islandRoom.AudioTracks.CreateAudioTrack(
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

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track published to Island Room");
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
                {
                    playbackSourcesHub.AddOrReplaceStream(key, stream);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Added existing remote track from {entry.Key}");
                }
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
                    {
                        playbackSourcesHub.AddOrReplaceStream(key, stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track subscribed from {participant.Identity}");
                    }
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track subscription: {ex.Message}");
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
                    playbackSourcesHub.RemoveStream(new StreamKey(participant.Identity, publication.Sid));
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track unsubscribed from {participant.Identity}");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track unsubscription: {ex.Message}");
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
                        playbackSourcesHub.AddOrReplaceStream(key, stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track loopback enabled (round-trip via server)");
                    }
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track published: {ex.Message}");
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
                    playbackSourcesHub.RemoveStream(new StreamKey(participant.Identity, publication.Sid));
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track loopback removed");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track unpublished: {ex.Message}");
                }
            }
        }

        private void Deactivate()
        {
            UnpublishLocalTrack();

            playbackSourcesHub.Stop();
            playbackSourcesHub.Reset();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Deactivated");
        }

        private void UnpublishLocalTrack()
        {
            if (localTrack != null && published)
            {
                try
                {
                    islandRoom.Participants.LocalParticipant().UnpublishTrack(localTrack, true);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track unpublished");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Error unpublishing: {ex.Message}");
                }
            }

            rtcAudioSource?.Dispose();
            rtcAudioSource = null;
            localTrack = null;
            published = false;
        }
    }
}
