using DCL.Prefs;
using DCL.Settings.ModuleViews;
using ECS.SceneLifeCycle.SingleScene;
using System.Collections.Generic;

namespace DCL.Settings.ModuleControllers
{
    public class SingleSceneModeSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly ISettingsModuleEventListener settingsEventListener;

        private bool isEnabled;

        public SingleSceneModeSettingsController(SettingsToggleModuleView view, SingleSceneMode singleSceneMode,
            ISettingsModuleEventListener settingsEventListener)
        {
            this.view = view;
            this.settingsEventListener = settingsEventListener;

            // The choice applies on the next launch, so show what was stored rather than what this session resolved
            isEnabled = DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_SINGLE_SCENE_MODE)
                ? DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_SINGLE_SCENE_MODE)
                : singleSceneMode.IsActive;

            view.ConfigureWithoutNotify(isEnabled);
            view.ToggleView.Toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        // Modules that react to this are built alongside this one, so the initial value goes out once they all exist
        public override void OnAllControllersInstantiated(List<SettingsFeatureController> controllers) =>
            settingsEventListener.NotifySingleSceneModeChanged(isEnabled);

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        private void OnToggleValueChanged(bool isOn)
        {
            isEnabled = isOn;
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_SINGLE_SCENE_MODE, isOn, true);
            settingsEventListener.NotifySingleSceneModeChanged(isOn);
        }
    }
}
