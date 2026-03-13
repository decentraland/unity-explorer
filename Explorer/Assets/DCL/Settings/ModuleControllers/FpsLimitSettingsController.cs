using DCL.Diagnostics;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class FpsLimitSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public FpsLimitSettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.DropdownView.Dropdown.value = FpsToDropdownIndex(qualitySettingsController.FpsLimit);
            view.DropdownView.Dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.DropdownView.Dropdown.SetValueWithoutNotify(FpsToDropdownIndex(qualitySettingsController.FpsLimit));
        }

        private void OnDropdownValueChanged(int index)
        {
            int fps = 0;

            if (index != 0 && !int.TryParse(view.DropdownView.Dropdown.options[index].text, out fps))
            {
                ReportHub.LogError(ReportCategory.SETTINGS_MENU, $"FPS limit option text is not a valid integer: {view.DropdownView.Dropdown.options[index].text}");
                return;
            }

            qualitySettingsController.SetFpsLimit(fps);
        }

        private int FpsToDropdownIndex(int fps)
        {
            if (fps == 0)
                return 0;

            for (int i = 1; i < view.DropdownView.Dropdown.options.Count; i++)
            {
                if (int.TryParse(view.DropdownView.Dropdown.options[i].text, out int optionFps) && optionFps == fps)
                    return i;
            }

            return 0;
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
