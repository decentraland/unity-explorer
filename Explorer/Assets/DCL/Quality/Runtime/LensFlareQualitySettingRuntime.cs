using UnityEngine;
using UnityEngine.Rendering;
using Utility;
using Utility.Storage;

namespace DCL.Quality.Runtime
{
    public partial class LensFlareQualitySettingRuntime : IQualitySettingRuntime
    {
        private LensFlareComponentSRP? instance;

        private PersistentSetting<bool> enabled;

        public bool IsActive => instance is { enabled: true };

        public void Dispose()
        {
            DestroyInstance();
        }

        public void SetActive(bool active)
        {
            if (instance != null)
                instance.enabled = active;

            enabled.Value = active;
        }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            enabled.Value = preset.lensFlareEnabled;

            DestroyInstance();

            if (preset.lensFlareEnabled)
                InstantiatePreset(preset);
        }

        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset)
        {
            enabled = PersistentSetting.CreateBool("LensFlare_Enabled", currentPreset.lensFlareEnabled);

            if (currentPreset.lensFlareEnabled)
                InstantiatePreset(currentPreset);
        }

        private void DestroyInstance()
        {
            if (instance != null)
            {
                UnityObjectUtils.SafeDestroyGameObject(instance);
                instance = null;
            }
        }

        private void InstantiatePreset(QualitySettingsAsset.QualityCustomLevel level)
        {
            instance = Object.Instantiate(level.lensFlareComponent, Vector3.zero, Quaternion.identity);
            instance.name = "Lens Flare (AutoGen)";
            instance.hideFlags = HideFlags.DontSave;
        }
    }
}
