using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Quality
{
    public class RendererFeaturesCache : IRendererFeaturesCache
    {
        /// <summary>
        ///     Renderer Features are not exposed in the API, so we need to use reflection to access them.
        /// </summary>
        private static readonly PropertyInfo RENDERER_FEATURES_PROPERTY = typeof(ScriptableRenderer).GetProperty("rendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly Dictionary<Type, ScriptableRendererFeature> cache = new (10);

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
                return (T)feature;

            var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            if (!asset) return null;

            ScriptableRenderer pipeline = asset.scriptableRenderer!;

            var features = (List<ScriptableRendererFeature>)RENDERER_FEATURES_PROPERTY.GetValue(pipeline)!;
            cache[typeof(T)] = feature = features.Find(f => f is T);
            return (T)feature;
        }

        private void OnQualityLevelChanged(int from, int to)
        {
            cache.Clear();
        }
    }
}
