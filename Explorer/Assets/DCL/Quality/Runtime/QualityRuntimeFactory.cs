using DCL.Landscape.Settings;
using DCL.LOD;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DCL.Quality.Runtime
{
    public static class QualityRuntimeFactory
    {
        public static IQualityLevelController Create(
            IRendererFeaturesCache rendererFeaturesCache,
            QualitySettingsAsset settingsAsset,
            RealmPartitionSettingsAsset? realmPartitionSettings = null,
            ILODSettingsAsset? lodSettingsAsset = null,
            LandscapeData? landscapeData = null)
        {
            var runtimes = new List<IQualitySettingRuntime>();

            runtimes.Add(CreateFogRuntime());
            runtimes.Add(CreateLensFlareRuntime());
            runtimes.Add(CreateGlobalVolume());
            runtimes.Add(CreateEnvironmentRuntime(realmPartitionSettings, lodSettingsAsset, landscapeData));
            CreateRendererFeaturesRuntimes(rendererFeaturesCache, settingsAsset, runtimes);

            return new QualityLevelController(runtimes, settingsAsset.customSettings);
        }

        private static IQualitySettingRuntime CreateLensFlareRuntime() =>
            new LensFlareQualitySettingRuntime();

        private static IQualitySettingRuntime CreateFogRuntime() =>
            new FogQualitySettingRuntime();

        private static IQualitySettingRuntime CreateEnvironmentRuntime(
            RealmPartitionSettingsAsset? realmPartitionSettings,
            ILODSettingsAsset? lodSettingsAsset,
            LandscapeData? landscapeData) =>
            new EnvironmentSettingsRuntime(realmPartitionSettings, lodSettingsAsset, landscapeData);

        /// <summary>
        ///     Create a separate class for every renderer feature type possibly available
        /// </summary>
        /// <param name="runtimes"></param>
        private static void CreateRendererFeaturesRuntimes(IRendererFeaturesCache rendererFeaturesCache, QualitySettingsAsset settingsAsset, List<IQualitySettingRuntime> runtimes)
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

            globalVol.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            Volume? volume = globalVol.GetComponent<Volume>();

            if (volume == null)
                volume = globalVol.AddComponent<Volume>();

            volume.isGlobal = true;

            return new VolumeProfileQualitySettingRuntime(volume);
        }
    }
}
