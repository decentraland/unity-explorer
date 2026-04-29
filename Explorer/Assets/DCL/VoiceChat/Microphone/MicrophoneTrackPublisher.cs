using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Settings.Settings;
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Tracks;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

#if UNITY_STANDALONE_OSX
using DCL.VoiceChat.Permissions;
#endif

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Manages local microphone track publishing lifecycle.
    ///     Used for both community and nearby voice chat.
    /// </summary>
    public class MicrophoneTrackPublisher : IDisposable
    {
        private static readonly TrackPublishOptions DEFAULT_PUBLISH_OPTIONS = new ()
        {
            AudioEncoding = new AudioEncoding { MaxBitrate = 124000 },
            Source = TrackSource.SourceMicrophone,
        };

        private const string MIC_VOLUME_NAME = nameof(AudioMixerExposedParam.Microphone_Volume);

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly string tag;

        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);

        private MicrophoneTrack? microphoneTrack;

        public Weak<MicrophoneRtcAudioSource> CurrentMicrophone => microphoneTrack?.Source ?? Weak<MicrophoneRtcAudioSource>.Null;
        internal bool isPublished => microphoneTrack.HasValue;
        internal bool isRecording => CurrentMicrophone.Resource is { Has: true, Value: { IsRecording: true } };

        /// <summary>
        ///     Raised whenever the active microphone source changes: a new source after successful publish,
        ///     or <see cref="Weak{T}.Null"/> after unpublish/cleanup (including the error path in <see cref="PublishAsync"/>).
        /// </summary>
        public event Action<Weak<MicrophoneRtcAudioSource>>? SourceChanged;

        private bool isDisposed;

        public MicrophoneTrackPublisher(
            IRoom voiceChatRoom,
            VoiceChatConfiguration configuration,
            VoiceChatType voiceChatType)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            tag = $"{nameof(MicrophoneTrackPublisher)}({voiceChatType})";
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            Unpublish();
            semaphoreSlim.Dispose();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Disposed");
        }

        /// <summary>
        ///     Publishes the local microphone track to the room.
        ///     Creates the <see cref="MicrophoneRtcAudioSource"/> via shared helper;
        ///     starts recording immediately when <paramref name="micAutoStart"/> is true — callers decide
        ///     whether the mic should be recording on publish based on their own state.
        /// </summary>
        public async UniTask PublishAsync(bool micAutoStart, CancellationToken ct)
        {
            using var _ = await semaphoreSlim.LockAsync();

            if (microphoneTrack.HasValue)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Local track already published");
                return;
            }

            try
            {
                LKParticipant localParticipant = voiceChatRoom.Participants.LocalParticipant();

                if (localParticipant == null)
                    throw new InvalidOperationException($"{tag} Local participant is not available yet");

                MicrophoneRtcAudioSource rtcAudioSource = await CreateMicrophoneSourceAsync(configuration, ct);

                if (micAutoStart)
                    rtcAudioSource.Start();

                ITrack track = voiceChatRoom.LocalTracks.CreateAudioTrack(localParticipant.Name, rtcAudioSource);

                microphoneTrack = new MicrophoneTrack(track, new Owned<MicrophoneRtcAudioSource>(rtcAudioSource));

                SourceChanged?.Invoke(microphoneTrack.Value.Source);

                localParticipant.PublishTrack(track, DEFAULT_PUBLISH_OPTIONS, ct);

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Local track published successfully");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{tag} Failed to publish local track: {ex.Message}");
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification("No Available Microphone"));
                CleanupLocalTrack();
                throw;
            }
        }

        public void Unpublish()
        {
            if (!microphoneTrack.HasValue) return;

            try
            {
                LKParticipant localParticipant = voiceChatRoom.Participants.LocalParticipant();

                if (localParticipant == null)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{tag} Cannot unpublish: local participant is not available");
                    return;
                }

                localParticipant.UnpublishTrack(microphoneTrack.Value.Track, true);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Local track unpublished");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{tag} Failed to unpublish: {ex.Message}"); }
            finally { CleanupLocalTrack(); }
        }

        internal void StartMicrophone()
        {
            Option<MicrophoneRtcAudioSource> source = CurrentMicrophone.Resource;
            if (source.Has) source.Value.Start();
        }

        internal void StopMicrophone()
        {
            Option<MicrophoneRtcAudioSource> source = CurrentMicrophone.Resource;
            if (source.Has) source.Value.Stop();
        }

        private void CleanupLocalTrack()
        {
            StopMicrophone();
            SourceChanged?.Invoke(Weak<MicrophoneRtcAudioSource>.Null);
            microphoneTrack?.Dispose();
            microphoneTrack = null;
        }

        private static async UniTask<MicrophoneRtcAudioSource> CreateMicrophoneSourceAsync(VoiceChatConfiguration configuration, CancellationToken ct)
        {
            if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
                configuration.AudioMixerGroup.audioMixer.SetFloat(MIC_VOLUME_NAME, 13);

#if UNITY_STANDALONE_OSX
            bool hasPermissions = await VoiceChatPermissions.GuardAsync(ct);

            if (!hasPermissions)
                throw new InvalidOperationException("Microphone permissions not granted");
#endif

            Result<MicrophoneSelection> reachable = VoiceChatSettings.ReachableSelection();

            if (!reachable.Success)
                throw new InvalidOperationException($"No microphone available: {reachable.ErrorMessage}");

            Result<MicrophoneRtcAudioSource> result = MicrophoneRtcAudioSource.New(
                reachable.Value,
                (configuration.AudioMixerGroup.audioMixer, MIC_VOLUME_NAME),
                configuration.microphonePlaybackToSpeakers);

            if (!result.Success)
                throw new InvalidOperationException($"Failed to create RTC audio source: {result.ErrorMessage}");

            return result.Value;
        }
    }
}
