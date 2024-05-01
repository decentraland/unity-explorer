using UnityEngine;
using Utility.Storage;

namespace DCL.Quality.Runtime
{
    public partial class FogQualitySettingRuntime : IQualitySettingRuntime
    {
        private PersistentSetting<bool> active;

        public bool IsActive => RenderSettings.fog;

        public void SetActive(bool active)
        {
            OverrideActive(active);
        }

        /// <summary>
        ///     This method is called on initialization to restore the persistent state
        /// </summary>
        /// <param name="currentPreset"></param>
        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset)
        {
            active = PersistentSetting.CreateBool("FogActive", currentPreset.fogSettings.m_Fog);

            // Apply RenderSettings
            RenderSettings.fog = active.Value;
        }

        internal void OverrideActive(bool active)
        {
            RenderSettings.fog = active;
            this.active.Value = active;
        }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            OverrideActive(preset.fogSettings.m_Fog);
        }
    }
}
