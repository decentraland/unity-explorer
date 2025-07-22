using DCL.DebugUtilities;
using DCL.SDKComponents.LightSource;
using System;
using System.Collections.Generic;

namespace DCL.Quality.Runtime
{
    public class DynamicLightsSettingsRuntime : IQualitySettingRuntime
    {
        private readonly LightSourceSettings? lightSourceSettings;

        public bool IsActive => true;

        public DynamicLightsSettingsRuntime(LightSourceSettings? lightSourceSettings)
        {
            this.lightSourceSettings = lightSourceSettings;
        }

        public void SetActive(bool active)
        {
        }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            lightSourceSettings?.ApplyQualitySettings(preset.dynamicLights.sceneLimitations);
        }

        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset)
        {
            ApplyPreset(currentPreset);
        }

        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate)
        {
        }
    }
}
