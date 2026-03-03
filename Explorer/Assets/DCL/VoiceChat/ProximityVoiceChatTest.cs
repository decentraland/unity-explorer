using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.Settings.Settings;
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
using System.Threading;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Temporary test: tries to publish an audio track to the Island Room
    ///     to verify if LiveKit server allows audio in that room.
    ///     Check logs for "[PROXIMITY_TEST]" prefix.
    /// </summary>
    public class ProximityVoiceChatTest : IDisposable
    {
        private const string TAG = "[PROXIMITY_TEST]";

        private readonly IRoom islandRoom;
        private readonly VoiceChatConfiguration configuration;

        private MicrophoneRtcAudioSource? rtcAudioSource;
        private ITrack? localTrack;
        private bool published;
        private bool disposed;

        public ProximityVoiceChatTest(IRoom islandRoom, VoiceChatConfiguration configuration)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;

            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Test initialized, waiting for Island Room connection...");

            if (islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                TryPublishAsync(CancellationToken.None).Forget();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            islandRoom.ConnectionUpdated -= OnConnectionUpdated;
            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;

            Cleanup();
            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Test disposed");
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate update, DisconnectReason? reason)
        {
            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Island Room connection: {update}");

            if (update == ConnectionUpdate.Connected && !published)
                TryPublishAsync(CancellationToken.None).Forget();

            if (update == ConnectionUpdate.Disconnected)
                Cleanup();
        }

        private async UniTaskVoid TryPublishAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            try
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} === ATTEMPTING TO PUBLISH AUDIO TRACK TO ISLAND ROOM ===");

                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                    configuration.AudioMixerGroup.audioMixer.SetFloat(nameof(AudioMixerExposedParam.Microphone_Volume), 13);

                Result<MicrophoneSelection> reachable = VoiceChatSettings.ReachableSelection();

                if (!reachable.Success)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} No microphone available: {reachable.ErrorMessage}");
                    return;
                }

                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Microphone found: {reachable.Value.name}");

                Result<MicrophoneRtcAudioSource> result = MicrophoneRtcAudioSource.New(
                    reachable.Value,
                    (configuration.AudioMixerGroup.audioMixer, nameof(AudioMixerExposedParam.Microphone_Volume)),
                    false
                );

                if (!result.Success)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to create RTC audio source: {result.ErrorMessage}");
                    return;
                }

                rtcAudioSource = result.Value;
                rtcAudioSource.Start();
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} RTC audio source started");

                string participantName = islandRoom.Participants.LocalParticipant().Name;
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Local participant: {participantName}");

                localTrack = islandRoom.AudioTracks.CreateAudioTrack($"proximity_{participantName}", rtcAudioSource);
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Audio track created, attempting PublishTrack...");

                var options = new TrackPublishOptions
                {
                    AudioEncoding = new AudioEncoding { MaxBitrate = 124000 },
                    Source = TrackSource.SourceMicrophone,
                };

                islandRoom.Participants.LocalParticipant().PublishTrack(localTrack, options, ct);

                published = true;
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} === SUCCESS: Audio track published to Island Room! ===");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} === FAILED: Cannot publish audio to Island Room: {ex.Message} ===\n{ex}");
            }
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} RECEIVED audio track from {participant.Identity} (sid: {publication.Sid})");
            else
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Received non-audio track from {participant.Identity}, kind: {publication.Kind}");
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Audio track UNSUBSCRIBED from {participant.Identity}");
        }

        private void Cleanup()
        {
            if (localTrack != null && published)
            {
                try
                {
                    islandRoom.Participants.LocalParticipant().UnpublishTrack(localTrack, true);
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Track unpublished");
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
