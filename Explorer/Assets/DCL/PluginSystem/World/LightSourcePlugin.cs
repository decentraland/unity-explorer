using Arch.SystemGroups;
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
    public class LightSourcePlugin : IDCLWorldPlugin<LightSourcePlugin.LightSourcePluginSettings>
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly ICharacterObject characterObject;
        private readonly Arch.Core.World globalWorld;
        private readonly bool hasDebugFlag;

        private LightSourceSettings? lightSourceSettings;
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
            LightSourceApplyPropertiesSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, sharedDependencies.ScenePartition, lightSourceSettings);
            LightSourceCullingSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, characterObject, lightSourceSettings);
            LightSourceLodSystem.InjectToWorld(ref builder, lightSourceSettings);
            LightSourceIntensityAnimationSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, lightSourceSettings);

            if (hasDebugFlag) LightSourceDebugSystem.InjectToWorld(ref builder, globalWorld);

            finalizeWorldSystems.Add(lifecycleSystem);
        }

        public async UniTask InitializeAsync(LightSourcePluginSettings settings, CancellationToken ct)
        {
            Light lightSourcePrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings!.LightSourcePrefab, ct)).Value.GetComponent<Light>();
            lightSourceSettings = (await assetsProvisioner.ProvideMainAssetAsync(settings.LightSourceSettings, ct)).Value;

            await CreateLightSourcePoolAsync(lightSourcePrefab, ct);
        }

        private async UniTask CreateLightSourcePoolAsync(Light lightSourcePrefab, CancellationToken ct)
        {
            lightPoolRegistry = poolsRegistry.AddGameObjectPool(() => Object.Instantiate(lightSourcePrefab, Vector3.zero, quaternion.identity), onRelease: OnPoolRelease, onGet: OnPoolGet);

            cacheCleaner.Register(lightPoolRegistry);
        }

        private void OnPoolRelease(Light light)
        {
            light.enabled = false;
            light.transform.SetParent(null);
        }

        private void OnPoolGet(Light light)
        {
            var defaultValues = lightSourceSettings!.DefaultValues;

            light.enabled = false;
            light.color = defaultValues.Color;
            light.intensity = 0;
            light.range = defaultValues.Range;
            light.innerSpotAngle = defaultValues.InnerAngle;
            light.spotAngle = defaultValues.OuterAngle;
            light.cookie = null;
        }

        public class LightSourcePluginSettings : IDCLPluginSettings
        {
            public AssetReferenceGameObject LightSourcePrefab;

            public AssetReferenceT<LightSourceSettings> LightSourceSettings;
        }
    }
}
