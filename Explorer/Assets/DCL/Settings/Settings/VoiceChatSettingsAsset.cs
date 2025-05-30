using UnityEngine;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        private const string CONNECTION_STRING_DATA_STORE_KEY = "Settings_ConnectionString";
        
        public delegate void MicrophoneChangedDelegate(int newMicrophoneIndex);
        public delegate void ConnectionStringChangedDelegate(string newConnectionString);

        public int SelectedMicrophoneIndex;
        public event MicrophoneChangedDelegate MicrophoneChanged;

        [Tooltip("Used for Debug Purposes")]
        public string ConnectionString;
        public event ConnectionStringChangedDelegate ConnectionStringChanged;

        private void OnEnable()
        {
            // Load saved connection string from PlayerPrefs when the asset is loaded
            LoadConnectionStringFromPlayerPrefs();
        }

        private void LoadConnectionStringFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(CONNECTION_STRING_DATA_STORE_KEY))
            {
                string savedConnectionString = PlayerPrefs.GetString(CONNECTION_STRING_DATA_STORE_KEY);
                if (!string.IsNullOrEmpty(savedConnectionString) && savedConnectionString != ConnectionString)
                {
                    ConnectionString = savedConnectionString;
                    ConnectionStringChanged?.Invoke(savedConnectionString);
                }
            }
        }

        public void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            SelectedMicrophoneIndex = newMicrophoneIndex;
            MicrophoneChanged?.Invoke(newMicrophoneIndex);
        }

        public void OnConnectionStringChanged(string newConnectionString)
        {
            ConnectionString = newConnectionString;
            ConnectionStringChanged?.Invoke(newConnectionString);
#if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(this);
    UnityEditor.AssetDatabase.SaveAssets();
#endif

        }
    }
}
