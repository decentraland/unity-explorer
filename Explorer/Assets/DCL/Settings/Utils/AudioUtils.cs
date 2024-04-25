using UnityEngine;

namespace DCL.Settings.Utils
{
    public static class AudioUtils
    {
        public static float PercentageVolumeToDecibel(float linear)
        {
            if (linear <= 0)
                return -80f;

            float dB = 20f * Mathf.Log10(linear / 100);
            return Mathf.Max(-80f, dB);
        }
    }
}
