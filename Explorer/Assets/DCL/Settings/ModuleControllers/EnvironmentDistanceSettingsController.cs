using DCL.Landscape.Settings;
using DCL.Prefs;
using DCL.Rendering.GPUInstancing;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class RoadsDistanceSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings roadsSettings;

        public RoadsDistanceSettingsController(SettingsSliderModuleView view, GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings roadsSettings)
        {
            this.view = view;
            this.roadsSettings = roadsSettings;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_ROADS_DISTANCE))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_ROADS_DISTANCE);

            view.SliderView.Slider.onValueChanged.AddListener(SetEnvironmentDistanceSettings);
            SetEnvironmentDistanceSettings(view.SliderView.Slider.value);
        }

        private void SetEnvironmentDistanceSettings(float distance) =>
            roadsSettings.RenderDistanceInParcels = distance;

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetEnvironmentDistanceSettings);
        }
    }


    public class EnvironmentDistanceSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly LandscapeData landscapeData;

        public EnvironmentDistanceSettingsController(SettingsSliderModuleView view, LandscapeData landscapeData)
        {
            this.view = view;
            this.landscapeData = landscapeData;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_ENVIRONMENT_DISTANCE))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_ENVIRONMENT_DISTANCE);

            view.SliderView.Slider.onValueChanged.AddListener(SetEnvironmentDistanceSettings);
            SetEnvironmentDistanceSettings(view.SliderView.Slider.value);

            landscapeData.OnDetailDistanceChanged += OnEnvironmentDistanceSettingsChangedFromOutside;
        }

        private void SetEnvironmentDistanceSettings(float distance) =>
            landscapeData.DetailDistance = distance;

        private void OnEnvironmentDistanceSettingsChangedFromOutside(float newDistance)
        {
            view.SliderView.Slider.value = newDistance;
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_ENVIRONMENT_DISTANCE, newDistance, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetEnvironmentDistanceSettings);
            landscapeData.OnDetailDistanceChanged -= OnEnvironmentDistanceSettingsChangedFromOutside;
        }
    }
}
