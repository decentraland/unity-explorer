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
using Utility;
using Utility.Multithreading;

#if UNITY_STANDALONE_OSX
using DCL.VoiceChat.Permissions;
#endif

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Manages local microphone track publishing lifecycle.
    /// </summary>
    public class MicrophoneTrackPublisher : IDisposable
    {
        private const string TAG = nameof(MicrophoneTrackPublisher);

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);

        private CancellationTokenSource? trackPublishingCts;
        private MicrophoneTrack? microphoneTrack;
        private bool isDisposed;

        public Weak<MicrophoneRtcAudioSource> CurrentMicrophone => microphoneTrack?.Source ?? Weak<MicrophoneRtcAudioSource>.Null;

        public MicrophoneTrackPublisher(
            IRoom voiceChatRoom,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.microphoneHandler = microphoneHandler;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            Unpublish();
            semaphoreSlim.Dispose();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        /// <summary>
        ///     Publishes the local microphone track to the room.
        ///     Creates and starts the MicrophoneRtcAudioSource if needed.
        /// </summary>
        public async UniTaskVoid PublishAsync(CancellationToken ct)
        {
            using var _ = await semaphoreSlim.LockAsync();

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
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification("No Available Microphone"));
                    throw new Exception(reachable.ErrorMessage!);
                }

                Result<MicrophoneRtcAudioSource> result = MicrophoneRtcAudioSource.New(
                    reachable.Value,
                    (configuration.AudioMixerGroup.audioMixer, nameof(AudioMixerExposedParam.Microphone_Volume)),
                    configuration.microphonePlaybackToSpeakers
                );

                if (!result.Success) throw new Exception($"Couldn't create RTCAudioSource: {result.ErrorMessage}");

                MicrophoneRtcAudioSource rtcAudioSource = result.Value;

                if (microphoneHandler.IsMicrophoneEnabled.Value)
                    rtcAudioSource.Start();

                ITrack livekitMicrophoneTrack = voiceChatRoom.LocalTracks.CreateAudioTrack(
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

        public void Unpublish()
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
                Track = track;
                this.source = source;
            }

            public void Dispose()
            {
                source.Dispose(out MicrophoneRtcAudioSource? inner);
                inner?.Dispose();
            }
        }
    }
}
