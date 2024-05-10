using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using UnityEngine;
using System.Threading;
using Utility;

namespace DCL.Audio.Avatar
{
    public class AvatarAudioPlaybackController : MonoBehaviour
    {
        [SerializeField] private AudioSource AvatarAudioSource;
        [SerializeField] private AudioSource ContinuousAudioAvatarAudioSource;
        [SerializeField] private AvatarAudioSettings AvatarAudioSettings;

        private CancellationTokenSource cancellationTokenSource;
        private bool playingContinuousAudio;

        private void Start()
        {
            AvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
            ContinuousAudioAvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            ContinuousAudioAvatarAudioSource.Stop();
            cancellationTokenSource?.SafeCancelAndDispose();
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
        }

        public void PlayContinuousAudio(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (!AvatarAudioSettings.AudioEnabled) return;

            if (!playingContinuousAudio)
            {
                AudioClipConfig clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);
                int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
                ContinuousAudioAvatarAudioSource.volume = clipConfig.RelativeVolume;
                ContinuousAudioAvatarAudioSource.clip = clipConfig.AudioClips[clipIndex];
                ContinuousAudioAvatarAudioSource.Play();
                playingContinuousAudio = true;

                cancellationTokenSource = new CancellationTokenSource();
                CancellationToken ct = cancellationTokenSource.Token;
                AudioPlaybackUtilities.SchedulePlaySoundAsync(ct, clipConfig, ContinuousAudioAvatarAudioSource.clip.length, ContinuousAudioAvatarAudioSource).Forget();
            }
        }

        public void PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (playingContinuousAudio)
            {
                playingContinuousAudio = false;
                ContinuousAudioAvatarAudioSource.Stop();
                cancellationTokenSource?.SafeCancelAndDispose();
                cancellationTokenSource = null;
            }

            if (!AvatarAudioSettings.AudioEnabled) return;

            AudioClipConfig clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);

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

            if (clipConfig.RelativeVolume == 0)
                return;

            AvatarAudioSource.pitch = AudioPlaybackUtilities.GetPitchWithVariation(clipConfig);
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
            AvatarAudioSource.PlayOneShot(clipConfig.AudioClips[clipIndex], clipConfig.RelativeVolume);
        }
    }
}
