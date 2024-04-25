using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public static class AudioPlaybackUtilities
    {
        private const int DEFAULT_PITCH = 1;

        public static float GetPitchWithVariation(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.PitchVariation > 0) { return DEFAULT_PITCH + Random.Range(-audioClipConfig.PitchVariation, audioClipConfig.PitchVariation); }

            return DEFAULT_PITCH;
        }

        public static int GetClipIndex(AudioClipConfig audioClipConfig, int startingClip = 0)
        {
            switch (audioClipConfig.ClipSelectionMode)
            {
                default:
                case AudioClipSelectionMode.First:
                    return startingClip;
                case AudioClipSelectionMode.Random:
                    return Random.Range(0, audioClipConfig.AudioClips.Length);
            }
        }

        public static async UniTask SchedulePlaySound(CancellationToken ct, AudioClipConfig clipConfig, float waitTime, AudioSource audioSource)
        {
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
            var clip = clipConfig.AudioClips[clipIndex];

            await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            audioSource.clip = clip;
            audioSource.Play();

            if (!ct.IsCancellationRequested)
            {
                SchedulePlaySound(ct, clipConfig, waitTime, audioSource).Forget();
            }
        }
    }
}
