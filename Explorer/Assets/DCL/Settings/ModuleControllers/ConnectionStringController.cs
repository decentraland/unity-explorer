using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class ConnectionStringController : SettingsFeatureController
    {
        private const string CONNECTION_STRING_DATA_STORE_KEY = "Settings_ConnectionString";

        private readonly SettingsTextModuleView view;
        private readonly VoiceChatSettingsAsset voiceChatSettings;

        public ConnectionStringController(SettingsTextModuleView view, VoiceChatSettingsAsset voiceChatSettings)
        {
            this.view = view;
            this.voiceChatSettings = voiceChatSettings;


            // Use the default value from the asset if no saved value exists
            view.InputField.text = voiceChatSettings.ConnectionString;

            // Listen for input field changes
            view.InputField.onEndEdit.AddListener(OnConnectionStringChanged);
        }

        private void OnConnectionStringChanged(string newConnectionString)
        {
            // Update the voice chat settings asset
            voiceChatSettings.OnConnectionStringChanged(newConnectionString);
        }

        public override void Dispose()
        {
            view.InputField.onEndEdit.RemoveListener(OnConnectionStringChanged);
        }
    }
}
