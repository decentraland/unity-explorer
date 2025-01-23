using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.LightSource.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.World
{
    public class LightSourcePlugin : IDCLWorldPlugin<LightSourcePlugin.Settings>
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private IComponentPool<Light>? lightPoolRegistry;

        public LightSourcePlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.poolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            finalizeWorldSystems.Add(LightSourceSystem.InjectToWorld(
                ref builder,
                lightPoolRegistry,
                sharedDependencies.SceneStateProvider
            ));

            ResetDirtyFlagSystem<PBLightSource>.InjectToWorld(ref builder);
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct) =>
            await CreateLightSourcePoolAsync(settings, ct);

        private async UniTask CreateLightSourcePoolAsync(Settings settings, CancellationToken ct)
        {
            Light light = (await assetsProvisioner.ProvideMainAssetAsync(settings.LightSourcePrefab, ct: ct)).Value.GetComponent<Light>();
            lightPoolRegistry = poolsRegistry.AddGameObjectPool(() => Object.Instantiate(light, Vector3.zero, quaternion.identity), onRelease: OnPoolRelease, onGet: OnPoolGet);
            cacheCleaner.Register(lightPoolRegistry);
        }

        private static void OnPoolRelease(Light light)
        {
            light.enabled = false;
            light.transform.SetParent(null);
            light.transform.localPosition = Vector3.zero;
            light.transform.localRotation = Quaternion.identity;

            light.color = Color.white;
            light.intensity = 1;
            light.range = 10;
            light.shadows = LightShadows.None;
            light.type = LightType.Point;

            light.innerSpotAngle = 12.8f;
            light.spotAngle = 30f;
        }

        private static void OnPoolGet(Light light) =>
            light.enabled = false;

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(LightSourcePlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject LightSourcePrefab;
        }
    }
}
