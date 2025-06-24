using UnityEngine;

namespace DCL.VoiceChat
{
    [CreateAssetMenu(fileName = "VoiceChatConfiguration", menuName = "DCL/Voice Chat/Voice Chat Configuration")]
    public class VoiceChatConfiguration : ScriptableObject
    {
        [Header("Local Audio Feedback")]
        [Tooltip("Enable to hear your own voice through the audio output (may cause echo)")]
        public bool EnableLocalTrackPlayback;
        [Header("Audio Echo Cancellation")]
        [Tooltip("Enable audio echo cancellation to prevent echo from speakers")]
        public bool EnableAudioEchoCancellation = true;

        [Tooltip("Correlation threshold for echo detection (lower = more sensitive)")]
        [Range(0.1f, 0.6f)]
        public float EchoCorrelationThreshold = 0.25f;

        [Tooltip("Maximum cancellation strength when echo is detected")]
        [Range(0.3f, 0.9f)]
        public float EchoCancellationStrength = 0.7f;

        [Tooltip("How quickly cancellation increases when echo is detected")]
        [Range(0.05f, 0.4f)]
        public float EchoCancellationAttackRate = 0.2f;

        [Tooltip("How quickly cancellation decreases when echo stops")]
        [Range(0.005f, 0.05f)]
        public float EchoCancellationReleaseRate = 0.02f;

        [Header("Voice Detection Configurations")]
        [Tooltip("Defines the threshold in seconds to identify push to talk or microphone toggle")]
        public float HoldThresholdInSeconds = 0.5f;

        [Header("General Settings")]
        [Tooltip("Enable or disable all audio processing (noise gate, filters, etc.)")]
        public bool EnableAudioProcessing = true;

        [Header("Noise Gate")]
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

        [Header("Low-Pass Filter")]
        [Tooltip("Enable low-pass filter to remove high-frequency noise")]
        public bool EnableLowPassFilter = true;

        [Tooltip("Low-pass filter cutoff frequency in Hz (removes high-frequency noise like hiss)")]
        [Range(3000f, 12000f)]
        public float LowPassCutoffFreq = 8000f;

        [Header("Gate Fade-In Settings")]
        [Tooltip("Enable smooth fade-in when noise gate opens to eliminate pops")]
        public bool EnableGateFadeIn = true;

        [Tooltip("Fade-in buffer size in samples (larger = smoother but more latency)")]
        [Range(32, 128)]
        public int FadeInBufferSize = 64;

        [Tooltip("Pre-gate audio attenuation factor during crossfade")]
        [Range(0.01f, 0.5f)]
        public float PreGateAttenuation = 0.1f;

        [Header("Auto-Gain Control")]
        [Tooltip("Target peak level for auto-gain (0.0 to 1.0)")]
        [Range(0.1f, 0.9f)]
        public float TargetPeakLevel = 0.7f;

        [Tooltip("Speed of gain adjustment (higher = faster response)")]
        [Range(1f, 10f)]
        public float GainAdjustSpeed = 4f;

        [Tooltip("Minimum gain multiplier")]
        [Range(0.1f, 2f)]
        public float MinGain = 1f;

        [Tooltip("Maximum gain multiplier")]
        [Range(10f, 100f)]
        public float MaxGain = 40f;

        [Tooltip("Minimum peak level to consider for gain adjustment")]
        [Range(0.00001f, 0.001f)]
        public float MinPeakThreshold = 0.0001f;

        [Tooltip("Window size in seconds for peak tracking")]
        [Range(0.1f, 2f)]
        public float PeakTrackingWindow = 0.5f;

        [Header("Microphone Initialization")]
        [Tooltip("Delay in milliseconds before reinitializing microphone after device change")]
        [Range(0, 1000)]
        public int MicrophoneReinitDelayMs = 500;

        [Tooltip("Maximum time in milliseconds to wait for fresh microphone data")]
        [Range(0, 2000)]
        public int MaxFreshDataWaitTimeMs = 1000;

        [Tooltip("Delay in milliseconds between checks for fresh microphone data")]
        [Range(10, 200)]
        public int FreshDataCheckDelayMs = 50;

        [Header("Reconnection Settings")]
        [Tooltip("Maximum number of reconnection attempts when connection is lost")]
        [Range(1, 10)]
        public int MaxReconnectionAttempts = 3;

        [Tooltip("Delay in milliseconds between reconnection attempts")]
        [Range(1000, 10000)]
        public int ReconnectionDelayMs = 2000;
    }
}
