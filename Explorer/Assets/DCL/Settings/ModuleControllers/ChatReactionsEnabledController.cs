using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class ChatReactionsEnabledController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly ChatSettingsAsset chatSettingsAsset;

        public ChatReactionsEnabledController(SettingsToggleModuleView view, ChatSettingsAsset chatSettingsAsset)
        {
            this.view = view;
            this.chatSettingsAsset = chatSettingsAsset;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_CHAT_REACTIONS_ENABLED))
                view.ToggleView.Toggle.isOn = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_CHAT_REACTIONS_ENABLED);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetReactionsEnabled);
            SetReactionsEnabled(view.ToggleView.Toggle.isOn);
        }

        private void SetReactionsEnabled(bool enabled)
        {
            chatSettingsAsset.SetReactionsEnabled(enabled);
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_CHAT_REACTIONS_ENABLED, enabled, save: true);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
    }
}