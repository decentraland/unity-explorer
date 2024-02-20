using UnityEngine.Rendering;

namespace DCL.Quality.Runtime
{
    public class VolumeProfileQualitySettingRuntime : IQualitySettingRuntime
    {
        // No persistence at the moment

        private readonly Volume globalVolume;

        public VolumeProfileQualitySettingRuntime(Volume globalVolume)
        {
            this.globalVolume = globalVolume;
        }

        public bool IsActive => globalVolume.enabled;

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
    }
}
