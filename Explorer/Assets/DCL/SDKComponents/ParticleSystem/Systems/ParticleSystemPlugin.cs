using System;
using System.Collections.Generic;
using System.Threading;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using Utility;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    public class ParticleSystemPlugin : IDCLWorldPlugin<ParticleSystemPlugin.ParticleSystemPluginSettings>
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly IDebugContainerBuilder debugBuilder;

        private IComponentPool<UnityEngine.ParticleSystem>? particleSystemPool;
        private IObjectPool<Material>? particleMaterialPool;
        private ParticleSystemPluginSettings? pluginSettings;
        private ElementBinding<string>? particleCountBinding;
        private DebugWidgetVisibilityBinding? particlesVisibilityBinding;

        public ParticleSystemPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner, IDebugContainerBuilder debugBuilder)
        {
            this.poolsRegistry = poolsRegistry;
            this.assetsProvisioner = assetsProvisioner;
            this.cacheCleaner = cacheCleaner;
            this.debugBuilder = debugBuilder;
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

            ParticleSystemLifecycleSystem.InjectToWorld(
                ref builder,
                sharedDependencies.SceneStateProvider,
                particleSystemPool);

            ParticleSystemApplyPropertiesSystem.InjectToWorld(
                ref builder,
                sharedDependencies.SceneData,
                sharedDependencies.ScenePartition,
                particleMaterialPool!);

            var playbackSystem = ParticleSystemPlaybackSystem.InjectToWorld(ref builder);
            sceneIsCurrentListeners.Add(playbackSystem);

            ParticleSystemBudgetSystem.InjectToWorld(ref builder, pluginSettings!, particleCountBinding!, particlesVisibilityBinding!);

            var cleanUpSystem = ParticleSystemCleanupSystem.InjectToWorld(
                ref builder,
                particleSystemPool,
                particleMaterialPool!);

            finalizeWorldSystems.Add(cleanUpSystem);
        }

        public async UniTask InitializeAsync(ParticleSystemPluginSettings settings, CancellationToken ct)
        {
            UnityEngine.ParticleSystem prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.ParticleSystemPrefab, ct)).Value
                                                                                                                                  .GetComponent<UnityEngine.ParticleSystem>();

            particleSystemPool = poolsRegistry.AddGameObjectPool(
                () => Object.Instantiate(prefab, Vector3.zero, quaternion.identity),
                onRelease: OnPoolRelease);

            cacheCleaner.Register(particleSystemPool);

            pluginSettings = settings;

            particleCountBinding = new ElementBinding<string>(string.Empty);
            particlesVisibilityBinding = new DebugWidgetVisibilityBinding(true);

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.PARTICLES)
                ?.SetVisibilityBinding(particlesVisibilityBinding)
                .AddCustomMarker("Scene Particles:", particleCountBinding);

            particleMaterialPool = new ObjectPool<Material>(
                createFunc: () => new Material(settings.ParticleMaterial),
                actionOnRelease: releasedMaterial => releasedMaterial.CopyPropertiesFromMaterial(settings.ParticleMaterial),
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                defaultCapacity: 64,
                maxSize: 512);
        }

        private static void OnPoolRelease(UnityEngine.ParticleSystem particleSystem)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.transform.SetParent(null);
            particleSystem.gameObject.SetActive(false);
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

            [field: SerializeField]
            public int MaxSceneParticles { get; private set; } = 1000;
        }
    }
}
