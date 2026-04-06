using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;


namespace DCL.Settings.ModuleControllers
{
    public class PointAtMarkerVisibilityController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings;

        public PointAtMarkerVisibilityController(
            SettingsDropdownModuleView view,
            PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings)
        {
            this.view = view;
            this.pointAtMarkerVisibilitySettings = pointAtMarkerVisibilitySettings;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_POINT_AT_MARKER_VISIBILITY))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_POINT_AT_MARKER_VISIBILITY);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetSettings);
        }

        public override void Dispose() =>
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSettings);

        private void SetSettings(int index)
        {
            switch (index)
            {
                case (int)PointAtMarkerVisibilitySettings.VisibilitySetting.FRIENDS_ONLY:
                    pointAtMarkerVisibilitySettings.SetMarkerVisibility(PointAtMarkerVisibilitySettings.VisibilitySetting.FRIENDS_ONLY);
                    break;
                case (int)PointAtMarkerVisibilitySettings.VisibilitySetting.ALL:
                    pointAtMarkerVisibilitySettings.SetMarkerVisibility(PointAtMarkerVisibilitySettings.VisibilitySetting.ALL);
                    break;
                case (int)PointAtMarkerVisibilitySettings.VisibilitySetting.NONE:
                    pointAtMarkerVisibilitySettings.SetMarkerVisibility(PointAtMarkerVisibilitySettings.VisibilitySetting.NONE);
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for PointAtMarkerVisibilityController: {index}");
                    return;
            }

            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_POINT_AT_MARKER_VISIBILITY, index, save: true);
        }
    }
}
