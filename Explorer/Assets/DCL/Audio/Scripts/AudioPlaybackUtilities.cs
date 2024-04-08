using UnityEngine;

namespace DCL.Audio
{
    public static class AudioPlaybackUtilities
    {
        private const int DEFAULT_PITCH = 1;
        public static float GetPitchWithVariation(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.PitchVariation > 0)
            {
                return DEFAULT_PITCH + Random.Range(-audioClipConfig.PitchVariation, audioClipConfig.PitchVariation);
            }
            else
            {
                return DEFAULT_PITCH;
            }
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


    }
}
