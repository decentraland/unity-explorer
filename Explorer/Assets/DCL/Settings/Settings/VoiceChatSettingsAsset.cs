using UnityEngine;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        public int SelectedMicrophoneIndex = 0;
    }
}
