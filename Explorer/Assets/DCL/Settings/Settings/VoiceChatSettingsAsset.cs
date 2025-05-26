using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        public int SelectedMicrophoneIndex = 0;

        public delegate void MicrophoneChangedDelegate(int newMicrophoneIndex);
        public event MicrophoneChangedDelegate? MicrophoneChanged;

        [Header("Voice detection configurations")]
        [Tooltip("Defines the window of analysis of the input voice to determine the loudness of the microphone")]
        public int SampleWindow = 64;

        [Tooltip("Defines the minimum loudness (wave form amplitude) to detect input to be sent via voice chat")]
        public float MicrophoneLoudnessMinimumThreshold = 0.2f;

        [Tooltip("Defines how many frames to wait between loudness checks")]
        public int LoudnessCheckFrameInterval = 5;

        [Tooltip("Defines the threshold in seconds to identify push to talk or microphone toggle")]
        public float HoldThresholdInSeconds = 0.5f;

        [Tooltip("Used for Debug Purposes")]
        public string ConnectionString;

        public void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            SelectedMicrophoneIndex = newMicrophoneIndex;
            MicrophoneChanged?.Invoke(newMicrophoneIndex);
        }
    }
}
