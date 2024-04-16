using UnityEngine;
using Utility.Storage;

namespace DCL.Quality.Runtime
{
    public partial class FogQualitySettingRuntime : IQualitySettingRuntime
    {
        private PersistentSetting<bool> active;
        private PersistentSetting<float> density;
        private PersistentSetting<float> endDistance;
        private PersistentSetting<Color> fogColor;
        private PersistentSetting<FogMode> fogMode;
        private PersistentSetting<float> startDistance;

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
            density = PersistentSetting.CreateFloat("FogDensity", currentPreset.fogSettings.m_FogDensity);
            endDistance = PersistentSetting.CreateFloat("FogEndDistance", currentPreset.fogSettings.m_LinearFogEnd);
            fogColor = PersistentSetting.CreateColor("FogColor", currentPreset.fogSettings.m_FogColor);
            fogMode = PersistentSetting.CreateEnum("FogMode", currentPreset.fogSettings.m_FogMode);
            startDistance = PersistentSetting.CreateFloat("FogStartDistance", currentPreset.fogSettings.m_LinearFogStart);

            // Apply RenderSettings
            RenderSettings.fog = active.Value;
            RenderSettings.fogColor = fogColor.Value;
            RenderSettings.fogMode = fogMode.Value;
            RenderSettings.fogDensity = density.Value;
            RenderSettings.fogStartDistance = startDistance.Value;
            RenderSettings.fogEndDistance = endDistance.Value;
        }

        internal float Density => RenderSettings.fogDensity;
        internal float EndDistance => RenderSettings.fogEndDistance;
        internal float StartDistance => RenderSettings.fogStartDistance;
        internal FogMode Mode => RenderSettings.fogMode;
        internal Color Color => RenderSettings.fogColor;

        internal void OverrideActive(bool active)
        {
            RenderSettings.fog = active;
            this.active.Value = active;
        }

        internal void OverrideDensity(float density)
        {
            RenderSettings.fogDensity = density;
            this.density.Value = density;
        }

        internal void OverrideEndDistance(float endDistance)
        {
            RenderSettings.fogEndDistance = endDistance;
            this.endDistance.Value = endDistance;
        }

        internal void OverrideStartDistance(float startDistance)
        {
            RenderSettings.fogStartDistance = startDistance;
            this.startDistance.Value = startDistance;
        }

        internal void OverrideMode(FogMode mode)
        {
            RenderSettings.fogMode = mode;
            fogMode.Value = mode;
        }

        internal void OverrideColor(Color color)
        {
            RenderSettings.fogColor = color;
            fogColor.Value = color;
        }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            // Override everything
            OverrideActive(preset.fogSettings.m_Fog);
            OverrideDensity(preset.fogSettings.m_FogDensity);
            OverrideEndDistance(preset.fogSettings.m_LinearFogEnd);
            OverrideStartDistance(preset.fogSettings.m_LinearFogStart);
            OverrideMode(preset.fogSettings.m_FogMode);
            OverrideColor(preset.fogSettings.m_FogColor);
        }
    }
}
