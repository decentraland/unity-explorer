using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Settings.Settings;
using DCL.Utilities.Extensions;
using DCL.VoiceChat.Permissions;
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
using Utility.Ownership;
using AudioStreamInfo = LiveKit.Rooms.Streaming.Audio.AudioStreamInfo;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Manages audio track publishing, subscribing, and lifecycle for voice chat.
    /// </summary>
    public class VoiceChatTrackManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatTrackManager);

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly PlaybackSourcesHub playbackSourcesHub;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly IDisposable microphoneChangeSubscription;

        private CancellationTokenSource? trackPublishingCts;
        private bool isDisposed;

        private MicrophoneTrack? microphoneTrack;
        private TrackPublication? currentTrackPublication;

        public Weak<MicrophoneRtcAudioSource> CurrentMicrophone => microphoneTrack?.Source ?? Weak<MicrophoneRtcAudioSource>.Null;

        public VoiceChatTrackManager(
            IRoom voiceChatRoom,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.microphoneHandler = microphoneHandler;

            microphoneChangeSubscription = microphoneHandler.IsMicrophoneEnabled.Subscribe(OnMicrophoneEnabledChanged);

            playbackSourcesHub = new PlaybackSourcesHub(
                new ConcurrentDictionary<StreamKey, LivekitAudioSource>(),
                configuration.ChatAudioMixerGroup.EnsureNotNull()
            );
        }

        private void OnMicrophoneEnabledChanged(bool isEnabled)
        {
            MuteLocalTrack(!isEnabled);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            UnpublishLocalTrack();
            StopListeningToRemoteTracks();
            microphoneChangeSubscription.Dispose();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        public void ActiveStreamsInfo(List<StreamInfo<AudioStreamInfo>> output)
        {
            voiceChatRoom.AudioStreams.ListInfo(output);
        }

        /// <summary>
        ///     Publishes the local microphone track to the room.
        ///     Creates and starts the OptimizedMonoRtcAudioSource if needed.
        /// </summary>
        public async UniTaskVoid PublishLocalTrackAsync(CancellationToken ct)
        {
            if (microphoneTrack.HasValue)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track already published");
                return;
            }

            //Raise volume if its Windows because for some reason Mac Volume is way higher than Windows.
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                configuration.AudioMixerGroup.audioMixer.SetFloat(nameof(AudioMixerExposedParam.Microphone_Volume), 13);

#if UNITY_STANDALONE_OSX
            bool hasPermissions = await VoiceChatPermissions.GuardAsync(ct);

            if (hasPermissions == false)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, "Microphone permissions were not granted by user, cannot publish local track");
                return;
            }
#endif

            try
            {
                Result<MicrophoneSelection> reachable = VoiceChatSettings.ReachableSelection();

                if (reachable.Success == false)
                {
                    NotificationsBus.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification("No Available Microphone"));
                    throw new Exception(reachable.ErrorMessage!);
                }

                Result<MicrophoneRtcAudioSource> result = MicrophoneRtcAudioSource.New(
                    reachable.Value,
                    (configuration.AudioMixerGroup.audioMixer, nameof(AudioMixerExposedParam.Microphone_Volume)),
                    configuration.microphonePlaybackToSpeakers
                );

                if (!result.Success) throw new Exception($"Couldn't create RTCAudioSource: {result.ErrorMessage}");

                var rtcAudioSource = result.Value;
                rtcAudioSource.Start();

                var livekitMicrophoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack(
                    voiceChatRoom.Participants.LocalParticipant().Name,
                    rtcAudioSource
                );

                microphoneTrack = new MicrophoneTrack(livekitMicrophoneTrack, new Owned<MicrophoneRtcAudioSource>(rtcAudioSource));
                microphoneHandler.Assign(microphoneTrack.Value.Source);

                var options = new TrackPublishOptions
                {
                    AudioEncoding = new AudioEncoding
                    {
                        MaxBitrate = 124000,
                    },
                    Source = TrackSource.SourceMicrophone,
                };

                voiceChatRoom.Participants.LocalParticipant().PublishTrack(microphoneTrack.Value.Track, options, ct);

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track published successfully");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to publish local track: {ex.Message}");
                CleanupLocalTrack();
                throw;
            }
        }

        private void MuteLocalTrack(bool mute)
        {
            if (!microphoneTrack.HasValue || currentTrackPublication == null) return;

            try
            {
                microphoneTrack.Value.Track.SetMute(mute);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track muted: {mute}");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to mute local track: {ex.Message}"); }
        }

        public void UnpublishLocalTrack()
        {
            if (microphoneTrack.HasValue)
                try
                {
                    voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack.Value.Track, true);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track unpublished");
                }
                catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to unpublish local track: {ex.Message}"); }
                finally { CleanupLocalTrack(); }
        }

        public void StartListeningToRemoteTracks()
        {
            try
            {
                playbackSourcesHub.Reset();

                foreach (var remoteParticipantIdentity in voiceChatRoom.Participants.RemoteParticipantIdentities())
                {
                    foreach ((string sid, TrackPublication value) in remoteParticipantIdentity.Value.Tracks)
                    {
                        if (value.Kind == TrackKind.KindAudio)
                        {
                            Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(new StreamKey(remoteParticipantIdentity.Key!, sid));

                            if (stream.Resource.Has)
                            {
                                playbackSourcesHub.AddOrReplaceStream(new StreamKey(remoteParticipantIdentity.Key!, sid), stream);
                                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Added existing remote track from {remoteParticipantIdentity}");
                            }
                        }
                    }
                }

                playbackSourcesHub.Play();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track listening started");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to start listening to remote tracks: {ex.Message}");
                throw;
            }
        }

        public void StopListeningToRemoteTracks()
        {
            StopListeningToRemoteTracksAsync().Forget();
        }

        private async UniTaskVoid StopListeningToRemoteTracksAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                playbackSourcesHub.Stop();
                playbackSourcesHub.Reset();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track listening stopped");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to stop listening to remote tracks: {ex.Message}"); }
        }

        public void HandleTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio)
                {
                    Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(new StreamKey(participant.Identity, publication.Sid));

                    if (stream.Resource.Has)
                    {
                        playbackSourcesHub.AddOrReplaceStream(new StreamKey(participant.Identity, publication.Sid), stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} New remote track subscribed from {participant.Identity}");
                    }
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track subscription: {ex.Message}"); }
        }

        public void HandleTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio)
                {
                    playbackSourcesHub.RemoveStream(new StreamKey(participant.Identity, publication.Sid));
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track unsubscribed from {participant.Identity}");
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track unsubscription: {ex.Message}"); }
        }

        public void HandleLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind != TrackKind.KindAudio) return;

                currentTrackPublication = publication;

                if (!configuration.EnableLocalTrackPlayback) return;

                WeakReference<IAudioStream>? stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                if (stream != null)
                {
                    playbackSourcesHub.AddOrReplaceStream(new StreamKey(participant.Identity), stream);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track added to playback (loopback enabled)");
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track published: {ex.Message}"); }
        }

        public void HandleLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind != TrackKind.KindAudio) return;

                currentTrackPublication = null;

                if (!configuration.EnableLocalTrackPlayback) return;

                playbackSourcesHub.RemoveStream(new StreamKey(participant.Identity));
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track removed from playback");

            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track unpublished: {ex.Message}"); }
        }

        private void CleanupLocalTrack()
        {
            microphoneTrack?.Dispose();
            microphoneTrack = null;
            trackPublishingCts?.SafeCancelAndDispose();
            trackPublishingCts = null;
        }

        private readonly struct MicrophoneTrack : IDisposable
        {
            private readonly Owned<MicrophoneRtcAudioSource> source;

            public ITrack Track { get; }

            public Weak<MicrophoneRtcAudioSource> Source => source.Downgrade();

            public MicrophoneTrack(ITrack track, Owned<MicrophoneRtcAudioSource> source)
            {
                this.Track = track;
                this.source = source;
            }

            public void Dispose()
            {
                source.Dispose(out var inner);
                inner?.Dispose();
            }
        }
    }
}
