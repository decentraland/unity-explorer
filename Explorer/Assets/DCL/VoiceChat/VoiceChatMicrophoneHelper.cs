using DCL.Diagnostics;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Helper utilities for microphone configuration and optimization
    /// </summary>
    public static class VoiceChatMicrophoneHelper
    {
        private const string TAG = nameof(VoiceChatMicrophoneHelper);

        // Voice chat sample rate constraints
        private const int MIN_ACCEPTABLE_SAMPLE_RATE = 8000;  // Below this is too low quality for voice
        private const int MAX_DESIRED_SAMPLE_RATE = 48000;    // Above this is unnecessary for voice chat
        private const int FALLBACK_SAMPLE_RATE = 48000;

        /// <summary>
        /// Determines the optimal sample rate for the given microphone device.
        /// Prioritizes Unity's output sample rate to avoid resampling, then chooses minimum viable rate.
        /// </summary>
        public static int GetOptimalMicrophoneSampleRate(string deviceName)
        {
            try
            {
                Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);
                int unityOutputRate = AudioSettings.outputSampleRate;

                if (unityOutputRate >= minFreq && unityOutputRate <= maxFreq)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"{TAG} Selected sample rate {unityOutputRate}Hz for microphone '{deviceName}' (matches Unity output rate, no resampling needed)");
                    return unityOutputRate;
                }

                int effectiveMin = Mathf.Max(minFreq, MIN_ACCEPTABLE_SAMPLE_RATE);
                int effectiveMax = Mathf.Min(maxFreq, MAX_DESIRED_SAMPLE_RATE);

                if (effectiveMin <= effectiveMax)
                {
                    // Use the minimum rate within our acceptable range for efficiency
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"{TAG} Selected sample rate {effectiveMin}Hz for microphone '{deviceName}' (Unity rate {unityOutputRate}Hz not supported, using minimum viable from range: {minFreq}-{maxFreq}Hz)");
                    return effectiveMin;
                }

                // Device doesn't support our preferred range
                if (maxFreq < MIN_ACCEPTABLE_SAMPLE_RATE)
                {
                    // Device max is too low, use it anyway
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"{TAG} Microphone '{deviceName}' maximum sample rate {maxFreq}Hz is below minimum acceptable {MIN_ACCEPTABLE_SAMPLE_RATE}Hz. Voice quality may be poor.");
                    return maxFreq;
                }
                else if (minFreq > MAX_DESIRED_SAMPLE_RATE)
                {
                    // Device min is too high, use minimum available
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"{TAG} Microphone '{deviceName}' minimum sample rate {minFreq}Hz exceeds our maximum desired {MAX_DESIRED_SAMPLE_RATE}Hz. Using minimum available.");
                    return minFreq;
                }
                else
                {
                    // This shouldn't happen, but fallback to device minimum
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"{TAG} Unexpected microphone range for '{deviceName}' (range: {minFreq}-{maxFreq}Hz). Using device minimum.");
                    return minFreq;
                }
            }
            catch (System.Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                    $"{TAG} Failed to query microphone capabilities for '{deviceName}': {ex.Message}. Using fallback rate {FALLBACK_SAMPLE_RATE}Hz.");
                return FALLBACK_SAMPLE_RATE;
            }
        }
    }
}
