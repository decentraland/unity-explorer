using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class AvatarOutlineSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public AvatarOutlineSettingsController(SettingsToggleModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.ToggleView.Toggle.onValueChanged.AddListener(SetAvatarOutlineEnabled);
            view.ConfigureWithoutNotify(qualitySettingsController.AvatarOutline);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.ConfigureWithoutNotify(qualitySettingsController.AvatarOutline);
        }

        private void SetAvatarOutlineEnabled(bool enabled)
        {
            qualitySettingsController.SetAvatarOutline(enabled);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
