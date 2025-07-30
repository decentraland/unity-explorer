using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        public delegate void MicrophoneChangedDelegate(string newMicrophoneName);
        public event MicrophoneChangedDelegate MicrophoneChanged;

        public string SelectedMicrophoneName;

        public void OnMicrophoneChanged(string newMicrophoneName)
        {
            SelectedMicrophoneName = newMicrophoneName;
            MicrophoneChanged?.Invoke(newMicrophoneName);
        }
    }
}
