using UnityEngine.Rendering.Universal;

namespace DCL.Quality
{
    public class RendererFeatureQualitySettingRuntime<T> : IQualitySettingRuntime where T: ScriptableRendererFeature
    {
        private readonly IRendererFeaturesCache rendererFeaturesCache;

        public RendererFeatureQualitySettingRuntime(IRendererFeaturesCache rendererFeaturesCache)
        {
            this.rendererFeaturesCache = rendererFeaturesCache;
        }

        public bool IsActive => rendererFeaturesCache.GetRendererFeature<T>()?.isActive ?? false;

        public void SetActive(bool active)
        {
            rendererFeaturesCache.GetRendererFeature<T>()?.SetActive(true);
        }
    }
}
