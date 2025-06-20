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

            landscapeData.OnEnvironmentDistanceChanged += OnEnvironmentDistanceSettingsChangedFromOutside;
        }

        private void SetEnvironmentDistanceSettings(float distance) =>
            landscapeData.EnvironmentDistance = distance;

        private void OnEnvironmentDistanceSettingsChangedFromOutside(float newDistance)
        {
            view.SliderView.Slider.value = newDistance;
            settingsDataStore.SetSliderValue(ENVIRONMENT_DISTANCE_DATA_STORE_KEY, newDistance, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetEnvironmentDistanceSettings);
            landscapeData.OnEnvironmentDistanceChanged -= OnEnvironmentDistanceSettingsChangedFromOutside;
        }
    }
}
