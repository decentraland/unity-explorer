using UnityEngine;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        public delegate void MicrophoneChangedDelegate(int newMicrophoneIndex);
        public delegate void ConnectionStringChangedDelegate(string newConnectionString);

        public int SelectedMicrophoneIndex;
        public event MicrophoneChangedDelegate MicrophoneChanged;

        [Tooltip("Used for Debug Purposes")]
        public string ConnectionString;
        public event ConnectionStringChangedDelegate ConnectionStringChanged;

        public void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            SelectedMicrophoneIndex = newMicrophoneIndex;
            MicrophoneChanged?.Invoke(newMicrophoneIndex);
        }

        public void OnConnectionStringChanged(string newConnectionString)
        {
            ConnectionString = newConnectionString;
            ConnectionStringChanged?.Invoke(newConnectionString);
        }
    }
}
