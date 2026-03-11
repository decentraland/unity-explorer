using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class MaxSceneLightsSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public MaxSceneLightsSettingsController(SettingsSliderModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.SliderView.Slider.onValueChanged.AddListener(OnSliderValueChanged);
            view.ConfigureWithoutNotify(qualitySettingsController.MaxSceneLights);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.MaxSceneLights);
        }

        private void OnSliderValueChanged(float value)
        {
            qualitySettingsController.SetMaxSceneLights((int)value);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}