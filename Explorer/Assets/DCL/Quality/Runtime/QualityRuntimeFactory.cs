using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DCL.Quality.Runtime
{
    public class QualityRuntimeFactory
    {
        private readonly IRendererFeaturesCache rendererFeaturesCache;

        public QualityRuntimeFactory(IRendererFeaturesCache rendererFeaturesCache)
        {
            this.rendererFeaturesCache = rendererFeaturesCache;
        }

        public IQualityLevelController Create(QualitySettingsAsset settingsAsset)
        {
            var runtimes = new List<IQualitySettingRuntime>();

            runtimes.Add(CreateFogRuntime());
            runtimes.Add(CreateGlobalVolume());
            CreateRendererFeaturesRuntimes(settingsAsset, runtimes);

            return new QualityLevelController(runtimes, settingsAsset.customSettings);
        }

        private static IQualitySettingRuntime CreateFogRuntime() =>
            new FogQualitySettingRuntime();

        /// <summary>
        ///     Create a separate class for every renderer feature type possibly available
        /// </summary>
        /// <param name="runtimes"></param>
        private void CreateRendererFeaturesRuntimes(QualitySettingsAsset settingsAsset, List<IQualitySettingRuntime> runtimes)
        {
            var processedTypes = new HashSet<Type>();

            foreach (ScriptableRendererFeature? feature in settingsAsset.allRendererFeatures)
            {
                if (feature == null)
                    continue;

                Type type = feature.GetType();

                if (!processedTypes.Add(type))
                    continue;

                Type runtimeType = typeof(RendererFeatureQualitySettingRuntime<>).MakeGenericType(type);
                var runtime = (IQualitySettingRuntime)Activator.CreateInstance(runtimeType, rendererFeaturesCache);
                runtimes.Add(runtime);
            }
        }

        private static IQualitySettingRuntime CreateGlobalVolume()
        {
            const string GLOBAL_VOLUME_NAME = "Global Volume (AutoGen)";

            GameObject[]? currentRootGOs = SceneManager.GetActiveScene().GetRootGameObjects();

            // Ensure global volume has been added
            GameObject? globalVol = currentRootGOs.FirstOrDefault(x => x.name == GLOBAL_VOLUME_NAME);

            if (globalVol == null)
                globalVol = new GameObject(GLOBAL_VOLUME_NAME);

            globalVol.hideFlags = HideFlags.NotEditable;

            Volume? volume = globalVol.GetComponent<Volume>();

            if (volume == null)
                volume = globalVol.AddComponent<Volume>();

            volume.isGlobal = true;

            return new VolumeProfileQualitySettingRuntime(volume);
        }
    }
}
