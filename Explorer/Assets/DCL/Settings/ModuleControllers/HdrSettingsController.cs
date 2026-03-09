using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class HdrSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public HdrSettingsController(SettingsToggleModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.ToggleView.Toggle.onValueChanged.AddListener(SetHdrEnabled);
            view.ConfigureWithoutNotify(qualitySettingsController.Hdr);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.Hdr);
        }

        private void SetHdrEnabled(bool enabled)
        {
            qualitySettingsController.SetHdr(enabled);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}