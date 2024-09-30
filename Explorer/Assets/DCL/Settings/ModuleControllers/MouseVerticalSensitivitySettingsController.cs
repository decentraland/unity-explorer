using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class MouseVerticalSensitivitySettingsController : SettingsFeatureController
    {
        private const string VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY = "Settings_VerticalMouseSensitivity";

        private readonly SettingsSliderModuleView view;

        public MouseVerticalSensitivitySettingsController(SettingsSliderModuleView view)
        {
            this.view = view;

            if (settingsDataStore.HasKey(VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetVerticalMouseSensitivity);
            SetVerticalMouseSensitivity(view.SliderView.Slider.value);
        }

        private void SetVerticalMouseSensitivity(float sensitivity)
        {
            //TODO: actual sensitivity set
            settingsDataStore.SetSliderValue(VERTICAL_MOUSE_SENSITIVITY_DATA_STORE_KEY, sensitivity, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetVerticalMouseSensitivity);
        }
    }
}
