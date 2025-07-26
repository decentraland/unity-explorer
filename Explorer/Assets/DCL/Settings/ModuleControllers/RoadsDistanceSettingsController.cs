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
                view.SliderView.Slider.value = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_ROADS_DISTANCE);

            view.SliderView.Slider.onValueChanged.AddListener(SetEnvironmentDistanceSettings);
            SetEnvironmentDistanceSettings(view.SliderView.Slider.value);

            roadsSettings.RoadsDistanceChanged += OnRoadsDistanceChangedFromOutside;
        }

        private void OnRoadsDistanceChangedFromOutside(int dist)
        {
            view.SliderView.Slider.value = dist;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_ROADS_DISTANCE, dist);
        }

        private void SetEnvironmentDistanceSettings(float distance)
        {
            var dist = (int)distance;
            roadsSettings.RenderDistanceInParcels = dist;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_ROADS_DISTANCE, dist);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetEnvironmentDistanceSettings);
            roadsSettings.RoadsDistanceChanged -= OnRoadsDistanceChangedFromOutside;
        }
    }
}
