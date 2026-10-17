using DCL.Quality;
using DCL.Quality.Runtime;
using System;

namespace DCL.Settings.ModuleControllers
{
    public class SimpleQualitySettingFeatureController : BaseQualitySettingsFeatureController
    {
        private readonly Action<IQualitySettingsController> onPresetChange;
        private readonly Action onDispose;

        public SimpleQualitySettingFeatureController(IQualitySettingsController qualitySettingsController,
            Action initialize,
            Action<IQualitySettingsController> onPresetChange,
            Action onDispose) : base(qualitySettingsController)
        {
            this.onPresetChange = onPresetChange;
            this.onDispose = onDispose;
            initialize?.Invoke();
        }

        protected override void OnPresetChanged(QualityPresetLevel _)
        {
            onPresetChange?.Invoke(qualitySettingsController);
        }

        public override void Dispose()
        {
            base.Dispose();
            onDispose?.Invoke();
        }
    }
}
