using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class ShadowDistanceSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public ShadowDistanceSettingsController(SettingsSliderModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.SliderView.Slider.onValueChanged.AddListener(OnSliderValueChanged);
            view.ConfigureWithoutNotify(qualitySettingsController.ShadowDistance);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.ShadowDistance);
        }

        private void OnSliderValueChanged(float value)
        {
            qualitySettingsController.SetShadowDistance((int)value);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}