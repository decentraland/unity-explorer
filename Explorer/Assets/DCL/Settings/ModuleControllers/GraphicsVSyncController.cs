using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using System.Collections.Generic;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsVSyncController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;
        private SettingsFeatureController fpsLimitController;

        public GraphicsVSyncController(SettingsToggleModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.ToggleView.Toggle.onValueChanged.AddListener(SetVSyncEnabled);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            ManualUpdate(qualitySettingsController.VSync);
        }

        private void SetVSyncEnabled(bool enabled)
        {
            qualitySettingsController.SetVSync(enabled);
            fpsLimitController?.SetViewInteractable(!enabled);
        }

        public override void OnAllControllersInstantiated(List<SettingsFeatureController> controllers)
        {
            foreach (var controller in controllers)
                if (controller is FpsLimitSettingsController fpsController)
                {
                    fpsLimitController = fpsController;
                    break;
                }

            ManualUpdate(qualitySettingsController.VSync);
        }

        private void ManualUpdate(bool active)
        {
            view.ConfigureWithoutNotify(active);
            fpsLimitController?.SetViewInteractable(!active);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
