using UnityEngine;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        public int SelectedMicrophoneIndex = 0;

        [Header("Voice detection configurations")]
        [Tooltip("Defines the window of analysis of the input voice to determine the loudness of the microphone")]
        public int SampleWindow = 64;

        [Tooltip("Defines the minimum loudness (wave form amplitude) to detect input to be sent via voice chat")]
        public float MicrophoneLoudnessMinimumThreshold = 0.2f;

        [Tooltip("Defines the threshold in seconds to identify push to talk or microphone toggle")]
        public float HoldThresholdInSeconds = 0.5f;
    }
}
