using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class SceneDistanceSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public SceneDistanceSettingsController(SettingsSliderModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.SliderView.Slider.onValueChanged.AddListener(OnSliderValueChanged);
            view.ConfigureWithoutNotify(qualitySettingsController.SceneDistance);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.SceneDistance);
        }

        private void OnSliderValueChanged(float distance)
        {
            qualitySettingsController.SetSceneDistance((int)distance);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}