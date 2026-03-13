using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class SceneLightsSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public SceneLightsSettingsController(SettingsToggleModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);
            view.ConfigureWithoutNotify(qualitySettingsController.SceneLights);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.SceneLights);
        }

        private void OnToggleValueChanged(bool enabled)
        {
            qualitySettingsController.SetSceneLights(enabled);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}