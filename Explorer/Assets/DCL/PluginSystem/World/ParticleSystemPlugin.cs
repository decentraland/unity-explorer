using System;
using System.Collections.Generic;
using System.Threading;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.ParticleSystem.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using Utility;

namespace DCL.PluginSystem.World
{
    public class ParticleSystemPlugin : IDCLWorldPlugin<ParticleSystemPlugin.ParticleSystemPluginSettings>
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;

        private IComponentPool<ParticleSystem>? particleSystemPool;
        private IObjectPool<Material>? particleMaterialPool;

        public ParticleSystemPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner, CacheCleaner cacheCleaner)
        {
            this.poolsRegistry = poolsRegistry;
            this.assetsProvisioner = assetsProvisioner;
            this.cacheCleaner = cacheCleaner;
        }

        public void Dispose() { }

        public void InjectToWorld(
            ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBParticleSystem>.InjectToWorld(ref builder);

            var lifecycleSystem = ParticleSystemLifecycleSystem.InjectToWorld(
                ref builder,
                sharedDependencies.SceneStateProvider,
                particleSystemPool,
                particleMaterialPool!);

            ParticleSystemApplyPropertiesSystem.InjectToWorld(
                ref builder,
                sharedDependencies.SceneData,
                sharedDependencies.ScenePartition,
                particleMaterialPool!);

            ParticleSystemPlaybackSystem.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(lifecycleSystem);
        }

        public async UniTask InitializeAsync(ParticleSystemPluginSettings settings, CancellationToken ct)
        {
            ParticleSystem prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.ParticleSystemPrefab, ct)).Value
                .GetComponent<ParticleSystem>();

            particleSystemPool = poolsRegistry.AddGameObjectPool(
                () => Object.Instantiate(prefab, Vector3.zero, quaternion.identity),
                onRelease: OnPoolRelease);

            cacheCleaner.Register(particleSystemPool);

            particleMaterialPool = new ObjectPool<Material>(
                createFunc: () => new Material(settings.ParticleMaterial),
                actionOnRelease: m => m.CopyPropertiesFromMaterial(settings.ParticleMaterial),
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                defaultCapacity: 64,
                maxSize: 512);
        }

        private static void OnPoolRelease(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(null);
            ps.gameObject.SetActive(false);
        }

        [Serializable]
        public class ParticleSystemPluginSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceGameObject ParticleSystemPrefab { get; private set; }

            /// <summary>
            ///     Base material for particle rendering. Should point to same Material used in MaterialWorldPlugin.
            ///     Kept as a direct reference (not Addressable) to ensure shader variants are compiled.
            /// </summary>
            [field: SerializeField]
            public Material ParticleMaterial { get; private set; }
        }
    }
}
