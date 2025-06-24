using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class MouseVerticalSensitivitySettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly ControlsSettingsAsset controlsSettingsAsset;

        public MouseVerticalSensitivitySettingsController(SettingsSliderModuleView view, ControlsSettingsAsset controlsSettingsAsset)
        {
            this.view = view;
            this.controlsSettingsAsset = controlsSettingsAsset;

            if (SettingsDataStore.HasKey(DCLPrefKeys.SETTINGS_VERTICAL_MOUSE_SENSITIVITY))
                view.SliderView.Slider.value = SettingsDataStore.GetSliderValue(DCLPrefKeys.SETTINGS_VERTICAL_MOUSE_SENSITIVITY);

            view.SliderView.Slider.onValueChanged.AddListener(SetVerticalMouseSensitivity);
            SetVerticalMouseSensitivity(view.SliderView.Slider.value);
        }

        private void SetVerticalMouseSensitivity(float sensitivity)
        {
            controlsSettingsAsset.VerticalMouseSensitivity = sensitivity;
            SettingsDataStore.SetSliderValue(DCLPrefKeys.SETTINGS_VERTICAL_MOUSE_SENSITIVITY, sensitivity, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveAllListeners();
        }
    }
}
