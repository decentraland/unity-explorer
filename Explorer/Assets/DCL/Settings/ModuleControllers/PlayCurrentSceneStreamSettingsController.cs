using DCL.Prefs;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleViews;
using System;
using System.Collections.Generic;

namespace DCL.Settings.ModuleControllers
{
    public class PlayCurrentSceneStreamSettingsController : BaseQualitySettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;

        public PlayCurrentSceneStreamSettingsController(SettingsToggleModuleView view, VideoPrioritizationSettings videoPrioritizationSettings, IQualitySettingsController qualitySettingsController) : base(qualitySettingsController)
        {
            this.view = view;
            this.videoPrioritizationSettings = videoPrioritizationSettings;

            view.ToggleView.Toggle.onValueChanged.AddListener(SetPlayCurrentSceneStream);
        }

        protected override void OnPresetChanged(QualityPresetLevel _)
        {
            ManualUpdate(qualitySettingsController.PlayCurrentSceneStreamsOnly);
        }

        private void SetPlayCurrentSceneStream(bool enabled)
        {
            qualitySettingsController.SetPlayCurrentSceneStreamsOnly(enabled);
            videoPrioritizationSettings.PlayCurrentSceneStreamOnly = enabled;
        }

        public override void OnAllControllersInstantiated(List<SettingsFeatureController> controllers)
        {
            ManualUpdate(qualitySettingsController.PlayCurrentSceneStreamsOnly);
        }

        private void ManualUpdate(bool active)
        {
            view.ConfigureWithoutNotify(active);
            videoPrioritizationSettings.PlayCurrentSceneStreamOnly = active;
        }

        public override void Dispose()
        {
            base.Dispose();
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
        }
    }
}
