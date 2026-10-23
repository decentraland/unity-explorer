using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Quality
{
    public class RendererFeaturesCache : IRendererFeaturesCache
    {
        private readonly Dictionary<Type, ScriptableRendererFeature?> cache = new (10);

        public RendererFeaturesCache()
        {
            QualitySettings.activeQualityLevelChanged += OnQualityLevelChanged;
        }

        public void Dispose()
        {
            QualitySettings.activeQualityLevelChanged -= OnQualityLevelChanged;
            cache.Clear();
        }

        public T? GetRendererFeature<T>() where T: ScriptableRendererFeature
        {
            if (cache.TryGetValue(typeof(T), out ScriptableRendererFeature? feature))
                return (T?)feature;

            var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            if (!asset) return null;

            // Reads the serialized renderer data instead of asset.scriptableRenderer, whose getter constructs the renderer
            // and calls Create() on every feature, which URP forbids before a pipeline instance exists (UUM-44048).
            feature = FindFeature<T>(asset.rendererDataList);
            cache[typeof(T)] = feature;
            return (T?)feature;
        }

        private static T? FindFeature<T>(ReadOnlySpan<ScriptableRendererData> rendererDataList) where T: ScriptableRendererFeature
        {
            foreach (ScriptableRendererData? rendererData in rendererDataList)
            {
                if (!rendererData) continue;

                List<ScriptableRendererFeature> features = rendererData!.rendererFeatures;

                for (var i = 0; i < features.Count; i++)
                    if (features[i] is T feature)
                        return feature;
            }

            return null;
        }

        private void OnQualityLevelChanged(int from, int to)
        {
            cache.Clear();
        }
    }
}
