using UnityEngine.Rendering.Universal;
using Utility.Storage;

namespace DCL.Quality.Runtime
{
    public partial class RendererFeatureQualitySettingRuntime<T> : IQualitySettingRuntime where T: ScriptableRendererFeature
    {
        private readonly IRendererFeaturesCache rendererFeaturesCache;

        private PersistentSetting<bool> active;

        public bool IsActive => rendererFeaturesCache.GetRendererFeature<T>()?.isActive ?? false;

        internal string name => rendererFeaturesCache.GetRendererFeature<T>()?.name ?? typeof(T).Name;

        public RendererFeatureQualitySettingRuntime(IRendererFeaturesCache rendererFeaturesCache)
        {
            this.rendererFeaturesCache = rendererFeaturesCache;
        }

        public void SetActive(bool active)
        {
            // When we override the active state do we need to store the original state so we can revert to it when the preset is applied?
            rendererFeaturesCache.GetRendererFeature<T>()?.SetActive(active);
            this.active.Value = active;
        }

        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset)
        {
            // Renderer Features comes directly from the currently selected quality asset (we can't override that)
            bool isActive = IsActive;

            active = PersistentSetting.CreateBool($"RendererFeature_{typeof(T).Name}", isActive);
            rendererFeaturesCache.GetRendererFeature<T>()?.SetActive(isActive);
        }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            active.Value = IsActive;
        }
    }
}
