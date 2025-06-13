using UnityEditor;
using UnityEngine;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //    [CreateAssetMenu(fileName = "VoiceChatSettings", menuName = "DCL/Settings/Voice Chat Settings")]
    public class VoiceChatSettingsAsset : ScriptableObject
    {
        public delegate void ConnectionStringChangedDelegate(string newConnectionString);
        public delegate void MicrophoneChangedDelegate(int newMicrophoneIndex);

        public event ConnectionStringChangedDelegate ConnectionStringChanged;
        public event MicrophoneChangedDelegate MicrophoneChanged;


        private const string CONNECTION_STRING_DATA_STORE_KEY = "Settings_ConnectionString";

        public int SelectedMicrophoneIndex;

        [Tooltip("Used for Debug Purposes")]
        public string ConnectionString;
        public string Token;
        public string RoomURL;

        private void OnEnable()
        {
            // Load saved connection string from PlayerPrefs when the asset is loaded
            LoadConnectionStringFromPlayerPrefs();
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
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
#endif
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
    }
}
