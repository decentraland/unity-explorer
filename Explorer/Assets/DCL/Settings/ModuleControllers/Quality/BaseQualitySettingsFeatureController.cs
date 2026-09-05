using DCL.Quality;
using DCL.Quality.Runtime;

namespace DCL.Settings.ModuleControllers
{
    public abstract class BaseQualitySettingsFeatureController : SettingsFeatureController
    {
        protected readonly IQualitySettingsController qualitySettingsController;

        protected BaseQualitySettingsFeatureController(IQualitySettingsController qualitySettingsController)
        {
            this.qualitySettingsController = qualitySettingsController;
            qualitySettingsController.OnPresetChanged += OnPresetChanged;
        }

        protected virtual void OnPresetChanged(QualityPresetLevel newPreset) { }

        public override void Dispose()
        {
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
