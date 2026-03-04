using UnityEngine;
using UnityEngine.Audio;

namespace DCL.VoiceChat
{
    //[CreateAssetMenu(fileName = "VoiceChatConfiguration", menuName = "DCL/Voice Chat/Voice Chat Configuration")]
    public class VoiceChatConfiguration : ScriptableObject
    {
        [Header("Local Audio Feedback")]
        [Tooltip("Enable to hear your own voice through the audio output (may cause echo)")]
        public bool EnableLocalTrackPlayback;

        [Header("Voice Detection Configurations")]
        [Tooltip("Defines the threshold in seconds to identify push to talk or microphone toggle")]
        public float HoldThresholdInSeconds = 0.5f;

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

        [Tooltip("Specify group where microphone should put its output")]
        public AudioMixerGroup AudioMixerGroup;

        [Tooltip("Enables playback your recording microphone back to your speakers, allows to ensure sanity of recording on your side. May produce echoes due APM filter is not applied on this step")]
        public bool microphonePlaybackToSpeakers;

        [Tooltip("Specify group where remote sources should put its output")]
        public AudioMixerGroup ChatAudioMixerGroup;

        [Header("Proximity Spatial Audio")]
        [Range(0f, 1f)]
        public float ProximitySpatialBlend = 1f;

        [Range(0f, 5f)]
        public float ProximityDopplerLevel;

        [Range(0f, 100f)]
        public float ProximityMinDistance = 2f;

        [Range(1f, 500f)]
        public float ProximityMaxDistance = 50f;

        [Range(0f, 360f)]
        public float ProximitySpread;

        public AudioRolloffMode ProximityRolloffMode = AudioRolloffMode.Logarithmic;

        public void ApplyProximitySettingsTo(AudioSource source)
        {
            source.spatialBlend = ProximitySpatialBlend;
            source.dopplerLevel = ProximityDopplerLevel;
            source.minDistance = ProximityMinDistance;
            source.maxDistance = ProximityMaxDistance;
            source.spread = ProximitySpread;
            source.rolloffMode = ProximityRolloffMode;
        }
    }
}
