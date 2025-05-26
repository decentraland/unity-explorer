using UnityEngine;

namespace DCL.VoiceChat
{
    [CreateAssetMenu(fileName = "VoiceChatConfiguration", menuName = "DCL/Voice Chat/Voice Chat Configuration")]
    public class VoiceChatConfiguration : ScriptableObject
    {
        [Header("Voice Detection Configurations")]
        [Tooltip("Defines the window of analysis of the input voice to determine the loudness of the microphone")]
        public int SampleWindow = 64;

        [Tooltip("Defines the minimum loudness (wave form amplitude) to detect input to be sent via voice chat")]
        public float MicrophoneLoudnessMinimumThreshold = 0.2f;

        [Tooltip("Defines the threshold in seconds to identify push to talk or microphone toggle")]
        public float HoldThresholdInSeconds = 0.5f;

        [Header("Noise Reduction & Audio Processing")]
        [Tooltip("Enable noise gate to cut off audio below a certain threshold")]
        public bool EnableNoiseGate = true;

        [Tooltip("Noise gate threshold - audio below this level will be completely muted")]
        [Range(0f, 1f)]
        public float NoiseGateThreshold = 0.005f;

        [Tooltip("Time in seconds to keep the gate open after speech ends (prevents cutting off word endings)")]
        [Range(0.1f, 2f)]
        public float NoiseGateHoldTime = 2f;

        [Tooltip("How quickly the gate opens when speech is detected (lower = faster)")]
        [Range(0.01f, 0.5f)]
        public float NoiseGateAttackTime = 0.05f;

        [Tooltip("How quickly the gate closes after hold time expires (lower = faster)")]
        [Range(0.01f, 1f)]
        public float NoiseGateReleaseTime = 0.1f;

        [Tooltip("Enable high-pass filter to remove low-frequency noise")]
        public bool EnableHighPassFilter = true;

        [Tooltip("High-pass filter cutoff frequency in Hz")]
        [Range(50f, 500f)]
        public float HighPassCutoffFreq = 250f;

        [Tooltip("Enable automatic gain control to normalize volume")]
        public bool EnableAutoGainControl = true;

        [Tooltip("Target volume level for automatic gain control")]
        [Range(0.1f, 1f)]
        public float AGCTargetLevel = 0.7f;

        [Tooltip("AGC response speed (higher = faster adjustment)")]
        [Range(0.1f, 5f)]
        public float AGCResponseSpeed = 1f;

        [Tooltip("Enable simple noise reduction using spectral subtraction")]
        public bool EnableNoiseReduction = true;

        [Tooltip("Noise reduction strength (0 = no reduction, 1 = maximum reduction)")]
        [Range(0f, 1f)]
        public float NoiseReductionStrength = 0.5f;
    }
} 