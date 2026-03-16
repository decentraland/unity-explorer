using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using DCL.Utilities.Extensions;

namespace DCL.Settings.ModuleControllers
{
    public class WindowModeSettingsController : BaseQualitySettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;

        public WindowModeSettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController) : base(qualitySettingsController)
        {
            this.view = view;

            int index = FullscreenModeUtils.Modes.IndexOf(qualitySettingsController.WindowMode);

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
            base.Dispose();
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(OnValueChanged);
        }
    }
}
