using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using UnityEngine;
using System.Threading;
using UnityEngine.Serialization;
using Utility;

namespace DCL.Audio.Avatar
{
    public class AvatarAudioPlaybackController : MonoBehaviour
    {
        [SerializeField] private AudioSource avatarAudioSource;
        [SerializeField] private AudioSource continuousAudioAvatarAudioSource;
        [SerializeField] private AvatarAudioSettings avatarAudioSettings;

        private CancellationTokenSource cancellationTokenSource;
        private bool playingContinuousAudio;

        private void Start()
        {
            avatarAudioSource.priority = avatarAudioSettings.AudioPriority;
            continuousAudioAvatarAudioSource.priority = avatarAudioSettings.AudioPriority;
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            continuousAudioAvatarAudioSource.Stop();
            cancellationTokenSource?.SafeCancelAndDispose();
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
        }

        public void PlayContinuousAudio(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (!avatarAudioSettings.AudioEnabled) return;

            if (!playingContinuousAudio)
            {
                AudioClipConfig clipConfig = avatarAudioSettings.GetAudioClipConfigForType(clipType);

                if (clipConfig.RelativeVolume == 0) return;

                int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
                continuousAudioAvatarAudioSource.volume = clipConfig.RelativeVolume;
                continuousAudioAvatarAudioSource.clip = clipConfig.AudioClips[clipIndex];
                continuousAudioAvatarAudioSource.Play();
                playingContinuousAudio = true;

                cancellationTokenSource = new CancellationTokenSource();
                CancellationToken ct = cancellationTokenSource.Token;
                AudioPlaybackUtilities.SchedulePlaySoundAsync(ct, clipConfig, continuousAudioAvatarAudioSource.clip.length, continuousAudioAvatarAudioSource).Forget();
            }
        }

        public void PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (playingContinuousAudio)
            {
                playingContinuousAudio = false;
                continuousAudioAvatarAudioSource.Stop();
                cancellationTokenSource?.SafeCancelAndDispose();
                cancellationTokenSource = null;
            }

            if (!avatarAudioSettings.AudioEnabled) return;

            AudioClipConfig clipConfig = avatarAudioSettings.GetAudioClipConfigForType(clipType);

            if (clipConfig == null)
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"Cannot Play Avatar Audio for {clipType} as it has no AudioClipConfig Assigned");
                return;
            }

            if (clipConfig.AudioClips.Length == 0)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.AUDIO), $"Cannot Play Avatar Audio for {clipType} as it has no Audio Clips Assigned");
                return;
            }

            if (clipConfig.RelativeVolume == 0) return;

            avatarAudioSource.pitch = AudioPlaybackUtilities.GetPitchWithVariation(clipConfig);
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
            avatarAudioSource.PlayOneShot(clipConfig.AudioClips[clipIndex], clipConfig.RelativeVolume);
        }
    }
}
