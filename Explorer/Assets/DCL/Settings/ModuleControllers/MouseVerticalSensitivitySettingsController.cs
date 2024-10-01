using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class MouseVerticalSensitivitySettingsController : SettingsFeatureController
    {
        private const string VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY = "Settings_VerticalMouseSensitivity";

        private readonly SettingsSliderModuleView view;
        private readonly ControlsSettingsAsset controlsSettingsAsset;

        public MouseVerticalSensitivitySettingsController(SettingsSliderModuleView view, ControlsSettingsAsset controlsSettingsAsset)
        {
            this.view = view;
            this.controlsSettingsAsset = controlsSettingsAsset;

            if (settingsDataStore.HasKey(VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetVerticalMouseSensitivity);
            SetVerticalMouseSensitivity(view.SliderView.Slider.value);
        }

        private void SetVerticalMouseSensitivity(float sensitivity)
        {
            controlsSettingsAsset.VerticalMouseSensitivity = sensitivity;
            settingsDataStore.SetSliderValue(VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY, sensitivity, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetVerticalMouseSensitivity);
        }
    }
}
