using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.Settings.Settings;
#if UNITY_STANDALONE_OSX
using DCL.VoiceChat.Permissions;
#endif
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Manages local microphone track publishing lifecycle for proximity voice chat.
    /// </summary>
    internal class ProximityMicrophoneTrackPublisher : IDisposable
    {
        private const string TAG = nameof(ProximityMicrophoneTrackPublisher);

        private static readonly TrackPublishOptions PUBLISH_OPTIONS = new ()
        {
            AudioEncoding = new AudioEncoding { MaxBitrate = 124000 },
            Source = TrackSource.SourceMicrophone,
        };

        private readonly IRoom islandRoom;
        private readonly VoiceChatConfiguration configuration;

        private MicrophoneRtcAudioSource? rtcAudioSource;
        private ITrack? localTrack;
        private bool published;

        internal bool isPublished => published;

        internal ProximityMicrophoneTrackPublisher(
            IRoom islandRoom,
            VoiceChatConfiguration configuration)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;

            VoiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;
        }

        public void Dispose()
        {
            VoiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;
            Unpublish();
        }

        internal async UniTask PublishAsync(CancellationToken ct)
        {
            if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
                configuration.AudioMixerGroup.audioMixer.SetFloat(nameof(AudioMixerExposedParam.Microphone_Volume), 13);

#if UNITY_STANDALONE_OSX
            bool hasPermissions = await VoiceChatPermissions.GuardAsync(ct);

            if (!hasPermissions)
            {
                ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Microphone permissions not granted");
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

            islandRoom.Participants.LocalParticipant().PublishTrack(localTrack, PUBLISH_OPTIONS, ct);
            published = true;

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track published");
        }

        internal void Unpublish()
        {
            if (localTrack != null && published)
            {
                try
                {
                    islandRoom.Participants.LocalParticipant().UnpublishTrack(localTrack, true);
                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track unpublished");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Error unpublishing: {ex.Message}");
                }
            }

            rtcAudioSource?.Dispose();
            rtcAudioSource = null;
            localTrack = null;
            published = false;
        }

        internal void StartMicrophone() => rtcAudioSource?.Start();

        internal void StopMicrophone() => rtcAudioSource?.Stop();

        private void OnMicrophoneChanged(MicrophoneSelection newSelection)
        {
            if (rtcAudioSource == null) return;

            SwitchMicrophoneAsync(newSelection).Forget();
            return;

            async UniTaskVoid SwitchMicrophoneAsync(MicrophoneSelection selection)
            {
                if (!PlayerLoopHelper.IsMainThread)
                    await UniTask.SwitchToMainThread();

                Result switchResult = rtcAudioSource!.SwitchMicrophone(selection);

                if (switchResult.Success)
                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Microphone switched to: {selection.name}");
                else
                    ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Failed to switch microphone: {switchResult.ErrorMessage}");
            }
        }
    }
}
