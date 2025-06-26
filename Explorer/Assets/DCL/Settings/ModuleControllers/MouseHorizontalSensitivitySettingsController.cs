using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class MouseHorizontalSensitivitySettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly ControlsSettingsAsset controlsSettingsAsset;

        public MouseHorizontalSensitivitySettingsController(SettingsSliderModuleView view, ControlsSettingsAsset controlsSettingsAsset)
        {
            this.view = view;
            this.controlsSettingsAsset = controlsSettingsAsset;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_HORIZONTAL_MOUSE_SENSITIVITY))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetSliderValue(DCLPrefKeys.SETTINGS_HORIZONTAL_MOUSE_SENSITIVITY);

            view.SliderView.Slider.onValueChanged.AddListener(SetHorizontalMouseSensitivity);
            SetHorizontalMouseSensitivity(view.SliderView.Slider.value);
        }

        private void SetHorizontalMouseSensitivity(float sensitivity)
        {
            controlsSettingsAsset.HorizontalMouseSensitivity = sensitivity;
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_HORIZONTAL_MOUSE_SENSITIVITY, sensitivity, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveAllListeners();
        }
    }
}
