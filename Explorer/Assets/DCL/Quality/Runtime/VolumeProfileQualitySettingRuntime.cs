using DCL.DebugUtilities;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Quality.Runtime
{
    public class VolumeProfileQualitySettingRuntime : IQualitySettingRuntime
    {
        // No persistence at the moment

        private readonly Volume globalVolume;

        public bool IsActive => globalVolume.enabled;

        public VolumeProfileQualitySettingRuntime(Volume globalVolume)
        {
            this.globalVolume = globalVolume;
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroyGameObject(globalVolume);
        }

        public void SetActive(bool active)
        {
            globalVolume.enabled = active;
        }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            globalVolume.sharedProfile = preset.volumeProfile;
        }

        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset)
        {
            globalVolume.sharedProfile = currentPreset.volumeProfile;
        }

        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate) { }
    }
}
