using DCL.FeatureFlags;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class LobbySettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly ISettingsModuleEventListener settingsEventListener;

        public LobbySettingsController(SettingsToggleModuleView view, ISettingsModuleEventListener settingsEventListener)
        {
            this.view = view;
            this.settingsEventListener = settingsEventListener;

            view.ConfigureWithoutNotify(FeaturesRegistry.Instance.LobbyEnabledSetting);
            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        private void OnToggleValueChanged(bool isOn)
        {
            bool previousValue = FeaturesRegistry.Instance.LobbyEnabledSetting;
            FeaturesRegistry.Instance.LobbyEnabledSetting = isOn;

            settingsEventListener.NotifyLobbyEnabledChanged(previousValue, isOn);
        }
    }
}
