using UnityEngine;

namespace DCL.VoiceChat
{
    [CreateAssetMenu(fileName = "VoiceChatConfiguration", menuName = "DCL/Voice Chat/Voice Chat Configuration")]
    public class VoiceChatConfiguration : ScriptableObject
    {
        [Header("Local Audio Feedback")]
        [Tooltip("Enable to hear your own voice through the audio output (may cause echo)")]
        public bool EnableLocalTrackPlayback = false;

        [Header("Voice Detection Configurations")]
        [Tooltip("Defines the threshold in seconds to identify push to talk or microphone toggle")]
        public float HoldThresholdInSeconds = 0.5f;

        [Header("General Settings")]
        [Tooltip("Enable or disable all audio processing (noise reduction, filters, etc.)")]
        public bool EnableAudioProcessing = true;

        [Header("Noise Reduction")]
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

        [Tooltip("Enable band-pass filter to isolate human voice frequencies")]
        public bool EnableBandPassFilter = true;

        [Tooltip("High-pass filter cutoff frequency in Hz (removes low-frequency noise like rumble)")]
        [Range(50f, 300f)]
        public float HighPassCutoffFreq = 80f;

        [Tooltip("Low-pass filter cutoff frequency in Hz (removes high-frequency noise like hiss)")]
        [Range(3000f, 12000f)]
        public float LowPassCutoffFreq = 8000f;

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

        [Header("Advanced Audio Processing")]
        [Tooltip("Enable de-esser to reduce harsh sibilant sounds (S, T, SH sounds)")]
        public bool EnableDeEsser = true;

        [Tooltip("De-esser threshold - sibilant sounds above this level will be compressed")]
        [Range(0.1f, 0.8f)]
        public float DeEsserThreshold = 0.3f;

        [Tooltip("De-esser compression ratio (higher = more aggressive)")]
        [Range(1f, 10f)]
        public float DeEsserRatio = 3f;

        [Header("Gate Fade-In Settings")]
        [Tooltip("Enable smooth fade-in when noise gate opens to eliminate pops")]
        public bool EnableGateFadeIn = true;

        [Tooltip("Fade-in buffer size in samples (larger = smoother but more latency)")]
        [Range(32, 128)]
        public int FadeInBufferSize = 64;

        [Tooltip("Pre-gate audio attenuation factor during crossfade")]
        [Range(0.01f, 0.5f)]
        public float PreGateAttenuation = 0.1f;

        [Header("Microphone Initialization")]
        [Tooltip("Delay in milliseconds before reinitializing microphone after device change")]
        [Range(0, 1000)]
        public int MicrophoneReinitDelayMs = 500;

        [Tooltip("Enable waiting for fresh microphone data after initialization")]
        public bool EnableFreshDataWait = true;

        [Tooltip("Maximum time in milliseconds to wait for fresh microphone data")]
        [Range(0, 2000)]
        public int MaxFreshDataWaitTimeMs = 1000;

        [Tooltip("Delay in milliseconds between checks for fresh microphone data")]
        [Range(10, 200)]
        public int FreshDataCheckDelayMs = 50;
    }
}
