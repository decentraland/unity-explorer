using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;

namespace DCL.Settings.ModuleControllers
{
    public class WindowModeSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public WindowModeSettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            int index = FullscreenModeUtils.IndexOf(qualitySettingsController.WindowMode);

            if (index >= 0)
                view.DropdownView.Dropdown.SetValueWithoutNotify(index);

            view.DropdownView.Dropdown.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnValueChanged(int index)
        {
            qualitySettingsController.SetWindowMode(index);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(OnValueChanged);
        }
    }
}
