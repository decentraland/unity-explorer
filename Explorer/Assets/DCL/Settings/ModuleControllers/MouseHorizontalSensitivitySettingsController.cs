using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class MouseHorizontalSensitivitySettingsController : SettingsFeatureController
    {
        private const string HORIZONTAL_MOUSE_SENSITIVITY_DATA_STORE_KEY = "Settings_HorizontalMouseSensitivity";

        private readonly SettingsSliderModuleView view;

        public MouseHorizontalSensitivitySettingsController(SettingsSliderModuleView view)
        {
            this.view = view;

            if (settingsDataStore.HasKey(HORIZONTAL_MOUSE_SENSITIVITY_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(HORIZONTAL_MOUSE_SENSITIVITY_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetHorizontalMouseSensitivity);
            SetHorizontalMouseSensitivity(view.SliderView.Slider.value);
        }

        private void SetHorizontalMouseSensitivity(float sensitivity)
        {
            //TODO: actual sensitivity set
            settingsDataStore.SetSliderValue(HORIZONTAL_MOUSE_SENSITIVITY_DATA_STORE_KEY, sensitivity, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetHorizontalMouseSensitivity);
        }
    }
}
