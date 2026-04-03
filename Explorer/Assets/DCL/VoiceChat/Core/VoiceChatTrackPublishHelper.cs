using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Settings.Settings;
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using System.Threading;
using UnityEngine;

#if UNITY_STANDALONE_OSX
using DCL.VoiceChat.Permissions;
#endif

namespace DCL.VoiceChat
{
    internal static class VoiceChatTrackPublishHelper
    {
        internal static readonly TrackPublishOptions DEFAULT_PUBLISH_OPTIONS = new ()
        {
            AudioEncoding = new AudioEncoding { MaxBitrate = 124000 },
            Source = TrackSource.SourceMicrophone,
        };

        private static string micVolumeName = nameof(AudioMixerExposedParam.Microphone_Volume);

        /// <summary>
        ///     Creates a <see cref="MicrophoneRtcAudioSource"/> ready for publishing.
        ///     Handles Windows volume workaround, macOS permission guard, and mic selection.
        ///     Does NOT call <c>Start()</c> — the caller decides when to activate.
        /// </summary>
        internal static async UniTask<MicrophoneRtcAudioSource> CreateMicrophoneSourceAsync(
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
