using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class DoubleTapToMoveSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;

        public DoubleTapToMoveSettingsController(SettingsToggleModuleView view)
        {
            this.view = view;

            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.DOUBLE_CLICK_WALK))
            {
                view.SetActive(false);
                return;
            }

            bool value = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_DOUBLE_TAP_TO_MOVE, false);
            view.ToggleView.Toggle.SetIsOnWithoutNotify(value);
            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        private static void OnToggleValueChanged(bool isOn) =>
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_DOUBLE_TAP_TO_MOVE, isOn, true);
    }
}
