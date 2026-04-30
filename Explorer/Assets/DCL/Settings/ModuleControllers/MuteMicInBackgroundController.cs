using DCL.Prefs;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class MuteMicInBackgroundController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;

        public MuteMicInBackgroundController(SettingsToggleModuleView view)
        {
            this.view = view;

            bool value = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MUTE_MIC_IN_BACKGROUND, true);
            view.ToggleView.Toggle.SetIsOnWithoutNotify(value);
            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        private static void OnToggleValueChanged(bool isOn) =>
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_MUTE_MIC_IN_BACKGROUND, isOn, true);
    }
}
