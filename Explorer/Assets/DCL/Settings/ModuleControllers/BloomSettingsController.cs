using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class BloomSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public BloomSettingsController(SettingsToggleModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.ToggleView.Toggle.onValueChanged.AddListener(SetBloomEnabled);
            view.ConfigureWithoutNotify(qualitySettingsController.Bloom);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.Bloom);
        }

        private void SetBloomEnabled(bool enabled)
        {
            qualitySettingsController.SetBloom(enabled);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}