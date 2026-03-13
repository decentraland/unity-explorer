using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class EnvironmentDistanceSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public EnvironmentDistanceSettingsController(SettingsSliderModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.SliderView.Slider.onValueChanged.AddListener(OnSliderValueChanged);
            view.ConfigureWithoutNotify(qualitySettingsController.LandscapeDistance);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.LandscapeDistance);
        }

        private void OnSliderValueChanged(float distance)
        {
            qualitySettingsController.SetLandscapeDistance(distance);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
