using UnityEngine;

namespace DCL.Audio
{
    public static class AudioPlaybackUtilities
    {
        public static float GetPitchVariation(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.pitchVariation > 0)
            {
                return Random.Range(-audioClipConfig.pitchVariation, audioClipConfig.pitchVariation);
            }
            else
            {
                return 0;
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
                    return Random.Range(0, audioClipConfig.audioClips.Length);
            }
        }


    }
}
