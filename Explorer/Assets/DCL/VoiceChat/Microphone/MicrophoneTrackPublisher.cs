using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Settings.Settings;
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
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
    ///     Used for both community and proximity voice chat.
    /// </summary>
    public class MicrophoneTrackPublisher : IDisposable
    {
        private static readonly TrackPublishOptions DEFAULT_PUBLISH_OPTIONS = new ()
        {
            AudioEncoding = new AudioEncoding { MaxBitrate = 124000 },
            Source = TrackSource.SourceMicrophone,
        };

        private static string micVolumeName = nameof(AudioMixerExposedParam.Microphone_Volume);

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly VoiceChatType voiceChatType;
        private readonly string tag;

        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);

        private MicrophoneTrack? microphoneTrack;

        public Weak<MicrophoneRtcAudioSource> CurrentMicrophone => microphoneTrack?.Source ?? Weak<MicrophoneRtcAudioSource>.Null;
        internal bool isPublished => microphoneTrack.HasValue;

        private bool isDisposed;

        public MicrophoneTrackPublisher(
            IRoom voiceChatRoom,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneHandler microphoneHandler,
            VoiceChatType voiceChatType)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.microphoneHandler = microphoneHandler;
            this.voiceChatType = voiceChatType;
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
        ///     conditionally starts it based on <paramref name="micAutoStart"/> and current mic-enabled state.
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
                MicrophoneRtcAudioSource rtcAudioSource = await CreateMicrophoneSourceAsync(configuration, ct);

                if (micAutoStart && microphoneHandler.IsMicrophoneEnabled.Value)
                    rtcAudioSource.Start();

                ITrack track = voiceChatRoom.LocalTracks.CreateAudioTrack(
                    voiceChatRoom.Participants.LocalParticipant().Name, rtcAudioSource);

                microphoneTrack = new MicrophoneTrack(track, new Owned<MicrophoneRtcAudioSource>(rtcAudioSource));
                microphoneHandler.Assign(microphoneTrack.Value.Source, voiceChatType);

                voiceChatRoom.Participants.LocalParticipant().PublishTrack(
                    track, DEFAULT_PUBLISH_OPTIONS, ct);

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
                voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack.Value.Track, true);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Local track unpublished");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{tag} Failed to unpublish: {ex.Message}"); }
            finally { CleanupLocalTrack(); }
        }

        private void CleanupLocalTrack()
        {
            microphoneHandler.ClearSource(voiceChatType);
            microphoneTrack?.Dispose();
            microphoneTrack = null;
        }

        private static async UniTask<MicrophoneRtcAudioSource> CreateMicrophoneSourceAsync(
            VoiceChatConfiguration configuration, CancellationToken ct)
        {
            micVolumeName = nameof(AudioMixerExposedParam.Microphone_Volume);

            if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
                configuration.AudioMixerGroup.audioMixer.SetFloat(micVolumeName, 13);

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
                (configuration.AudioMixerGroup.audioMixer, micVolumeName),
                configuration.microphonePlaybackToSpeakers);

            if (!result.Success)
                throw new InvalidOperationException(
                    $"Failed to create RTC audio source: {result.ErrorMessage}");

            return result.Value;
        }
    }
}
