using Arch.SystemGroups;
using CrdtEcsBridge.Components.Conversion;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.LightSource;
using DCL.SDKComponents.LightSource.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.World
{
    public class LightSourcePlugin : IDCLWorldPlugin<LightSourcePlugin.LightSourceSettings>
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly ICharacterObject characterObject;
        private readonly Arch.Core.World globalWorld;
        private readonly bool hasDebugFlag;

        private LightSourceSettings? pluginSettings;
        private LightSourceDefaults? lightSourceDefaults;
        private IComponentPool<Light>? lightPoolRegistry;

        public LightSourcePlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner,
            ICharacterObject characterObject,
            Arch.Core.World globalWorld,
            bool hasDebugFlag)
        {
            this.poolsRegistry = poolsRegistry;
            this.assetsProvisioner = assetsProvisioner;
            this.cacheCleaner = cacheCleaner;
            this.characterObject = characterObject;
            this.globalWorld = globalWorld;
            this.hasDebugFlag = hasDebugFlag;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(
            ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBLightSource>.InjectToWorld(ref builder);

            var lifecycleSystem = LightSourceLifecycleSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, lightPoolRegistry);
            LightSourceApplyPropertiesSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, sharedDependencies.ScenePartition);
            LightSourceCullingSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, characterObject, pluginSettings!.LightsPerParcel, pluginSettings.HardMaxLightCount, pluginSettings.MaxPointLightShadows, pluginSettings.MaxSpotLightShadows);
            LightSourceLodSystem.InjectToWorld(ref builder, pluginSettings.SpotLightsLods, pluginSettings.PointLightsLods);
            LightSourcePostCullingUpdateSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, pluginSettings.FadeDuration);

            if (hasDebugFlag) LightSourceDebugSystem.InjectToWorld(ref builder, globalWorld);

            finalizeWorldSystems.Add(lifecycleSystem);
        }

        public async UniTask InitializeAsync(LightSourceSettings settings, CancellationToken ct)
        {
            pluginSettings = settings;

            await CreateLightSourcePoolAsync(ct);
        }

        private async UniTask CreateLightSourcePoolAsync(CancellationToken ct)
        {
            lightSourceDefaults = (await assetsProvisioner.ProvideMainAssetAsync(pluginSettings.LightSourceDefaultValues, ct: ct)).Value;

            Light lightPrefab = (await assetsProvisioner.ProvideMainAssetAsync(pluginSettings.LightSourcePrefab, ct: ct)).Value.GetComponent<Light>();
            lightPoolRegistry = poolsRegistry.AddGameObjectPool(() => Object.Instantiate(lightPrefab, Vector3.zero, quaternion.identity), onRelease: OnPoolRelease, onGet: OnPoolGet);

            cacheCleaner.Register(lightPoolRegistry);
        }

        private void OnPoolRelease(Light light)
        {
            light.enabled = false;
        }

        private void OnPoolGet(Light light)
        {
            light.enabled = lightSourceDefaults!.active;
            light.transform.SetParent(null);
            light.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            light.color = lightSourceDefaults.color;
            light.intensity = PrimitivesConversionExtensions.PBBrightnessInLumensToUnityCandels(lightSourceDefaults.brightness);
            light.range = lightSourceDefaults.range;
            light.type = LightType.Spot;
            light.shadows = LightShadows.None;
            light.innerSpotAngle = lightSourceDefaults.innerAngle;
            light.spotAngle = lightSourceDefaults.outerAngle;
            light.cookie = null;
        }

        public class LightSourceSettings : IDCLPluginSettings
        {
            public float LightsPerParcel = 1;

            public int HardMaxLightCount = 10;

            public int MaxPointLightShadows = 1;

            public int MaxSpotLightShadows = 3;

            public float FadeDuration = 0.25f;

            public AssetReferenceGameObject LightSourcePrefab;

            public AssetReferenceT<LightSourceDefaults> LightSourceDefaultValues;

            public List<LightSourceLodSystem.LodSettings> SpotLightsLods;

            public List<LightSourceLodSystem.LodSettings> PointLightsLods;
        }
    }
}
