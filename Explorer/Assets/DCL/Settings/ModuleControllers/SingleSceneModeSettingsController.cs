using DCL.Prefs;
using DCL.Settings.ModuleViews;
using ECS.SceneLifeCycle.SingleScene;

namespace DCL.Settings.ModuleControllers
{
    public class SingleSceneModeSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;

        public SingleSceneModeSettingsController(SettingsToggleModuleView view, SingleSceneMode singleSceneMode)
        {
            this.view = view;

            // The choice applies on the next launch, so show what was stored rather than what this session resolved
            view.ConfigureWithoutNotify(singleSceneMode.IsActive);

            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        private static void OnToggleValueChanged(bool isOn) =>
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_SINGLE_SCENE_MODE, isOn, true);
    }
}
