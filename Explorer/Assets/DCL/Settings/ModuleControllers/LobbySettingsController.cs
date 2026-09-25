using DCL.FeatureFlags;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class LobbySettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;

        public LobbySettingsController(SettingsToggleModuleView view)
        {
            this.view = view;

            view.ConfigureWithoutNotify(FeaturesRegistry.Instance.LobbyEnabledSetting);
            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        private static void OnToggleValueChanged(bool isOn) =>
            FeaturesRegistry.Instance.LobbyEnabledSetting = isOn;
    }
}
