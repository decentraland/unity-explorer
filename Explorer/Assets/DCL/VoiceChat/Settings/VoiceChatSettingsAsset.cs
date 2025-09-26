using LiveKit.Runtime.Scripts.Audio;
using UnityEngine;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        public delegate void MicrophoneChangedDelegate(MicrophoneSelection newMicrophoneSelection);
        public event MicrophoneChangedDelegate MicrophoneChanged;

        public MicrophoneSelection? SelectedMicrophone;

        public void OnMicrophoneChanged(MicrophoneSelection microphoneSelection)
        {
            SelectedMicrophone = microphoneSelection;
            MicrophoneChanged?.Invoke(microphoneSelection);
        }
    }
}
