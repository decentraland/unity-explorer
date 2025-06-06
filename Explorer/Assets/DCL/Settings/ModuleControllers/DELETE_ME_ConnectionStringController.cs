using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class DELETE_ME_ConnectionStringController : SettingsFeatureController
    {
        private const string CONNECTION_STRING_DATA_STORE_KEY = "Settings_ConnectionString";

        private readonly DELETE_ME_SettingsTextModuleView view;
        private readonly VoiceChatSettingsAsset voiceChatSettings;

        public DELETE_ME_ConnectionStringController(DELETE_ME_SettingsTextModuleView view, VoiceChatSettingsAsset voiceChatSettings)
        {
            this.view = view;
            this.voiceChatSettings = voiceChatSettings;

            if (settingsDataStore.HasKey(CONNECTION_STRING_DATA_STORE_KEY))
            {
                string savedConnectionString = settingsDataStore.GetStringValue(CONNECTION_STRING_DATA_STORE_KEY);
                view.InputField.text = savedConnectionString;
                voiceChatSettings.OnConnectionStringChanged(savedConnectionString);
            }
            else if (!string.IsNullOrEmpty(voiceChatSettings.ConnectionString)) { view.InputField.text = voiceChatSettings.ConnectionString; }

            view.InputField.onEndEdit.AddListener(OnConnectionStringChanged);
        }

        private void OnConnectionStringChanged(string newConnectionString)
        {
            settingsDataStore.SetStringValue(CONNECTION_STRING_DATA_STORE_KEY, newConnectionString, true);
            voiceChatSettings.OnConnectionStringChanged(newConnectionString);
        }

        public override void Dispose()
        {
            view.InputField.onEndEdit.RemoveListener(OnConnectionStringChanged);
        }
    }
}
