using DCL.Landscape.Settings;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class EnvironmentDistanceSettingsController : SettingsFeatureController
    {
        private const string ENVIRONMENT_DISTANCE_DATA_STORE_KEY = "Settings_EnvironmentDistance";

        private readonly SettingsSliderModuleView view;
        private readonly LandscapeData landscapeData;

        public EnvironmentDistanceSettingsController(SettingsSliderModuleView view, LandscapeData landscapeData)
        {
            this.view = view;
            this.landscapeData = landscapeData;

            if (settingsDataStore.HasKey(ENVIRONMENT_DISTANCE_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(ENVIRONMENT_DISTANCE_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetEnvironmentDistanceSettings);
            SetEnvironmentDistanceSettings(view.SliderView.Slider.value);
        }

        private void SetEnvironmentDistanceSettings(float distance)
        {
            landscapeData.detailDistance = distance;
            settingsDataStore.SetSliderValue(ENVIRONMENT_DISTANCE_DATA_STORE_KEY, distance, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetEnvironmentDistanceSettings);
        }
    }
}
