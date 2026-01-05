using DCL.Prefs;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class HeadSyncController : SettingsFeatureController
    {
        private SettingsToggleModuleView view;

        public HeadSyncController(SettingsToggleModuleView view)
        {
            this.view = view;

            bool value = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_HEAD_SYNC_ENABLED, true);
            view.ToggleView.Toggle.SetIsOnWithoutNotify(value);
            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);

            Apply(value);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        private void OnToggleValueChanged(bool isOn) =>
            Apply(isOn);

        private static void Apply(bool headSyncEnabled) =>
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_HEAD_SYNC_ENABLED, headSyncEnabled, true);
    }
}
