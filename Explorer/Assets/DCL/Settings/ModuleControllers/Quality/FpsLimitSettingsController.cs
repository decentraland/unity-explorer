using DCL.Diagnostics;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class FpsLimitSettingsController : BaseQualitySettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;

        public FpsLimitSettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController) : base(qualitySettingsController)
        {
            this.view = view;

            view.DropdownView.Dropdown.value = FpsToDropdownIndex(qualitySettingsController.FpsLimit);
            view.DropdownView.Dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        protected override void OnPresetChanged(QualityPresetLevel _)
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
            base.Dispose();
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }
    }
}
