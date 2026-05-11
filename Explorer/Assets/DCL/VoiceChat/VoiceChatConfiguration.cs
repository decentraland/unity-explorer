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

        [Header("NEARBY")]
        [Tooltip("Hard cap on live pool-managed Nearby audio sources. Beyond this, Create falls back to the legacy " +
                 "instantiate-on-Create / destroy-on-Dispose path so the resident set drains naturally as users go out of range. " +
                 "Set to 0 to bypass pooling entirely.")]
        [Range(0, 2000)]
        public int NearbyMaxLiveInstances = 300;

        public AnimationCurve NearbyCustomRolloffCurve = new (
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(3f, 1f, 0f, 0f),
            new Keyframe(8f, 0.5f, -0.15f, -0.15f),
            new Keyframe(14f, 0.03f, -0.04f, -0.02f),
            new Keyframe(16f, 0f, -0.01f, 0f)
        );

        [Header("NEARBY - LiveKit Spatial")]
        public bool nearbySpatialize = true;
        [Range(0f, 1f)] public float nearbyIldStrength = 0.75f;
        public bool nearbySmoothPanning;

        [Header("NEARBY - SFX")]
        [Tooltip("When false (default), start/stop speaking SFX is muted whenever the OPEN_MIC transition was triggered by push-to-talk. " +
                 "Exposed in the Nearby Voice Chat debug widget so UX can A/B test push-to-talk sessions in-game.")]
        public bool nearbyPlaySfxOnPushToTalk;

        [Header("NEARBY - Audible Range (meters)")]
        [Tooltip("Audible-range hysteresis band, in meters.\n" +
                 "X = inner radius — crossing inward below this distance ADDS the audible-range tag.\n" +
                 "Y = outer radius — crossing outward beyond this distance REMOVES the audible-range tag.\n" +
                 "Must satisfy: Y > X > AudibleSuspendBand.Y, so bands strictly nest.")]
        public Vector2 nearbyAudibleRangeBand = new (18f, 22f);

        [Tooltip("Suspend hysteresis band, in meters.\n" +
                 "X = inner radius — crossing inward below this distance CLEARS the suspended mark.\n" +
                 "Y = outer radius — crossing outward beyond this distance MARKS the source suspended.\n" +
                 "Must satisfy: AudibleRangeBand.X > Y > X > 0.")]
        public Vector2 nearbyAudibleSuspendBand = new (16f, 17f);
    }
}
