// WebGL-specific GLTF Container Plugin that uses ONLY Asset Bundles
// Raw GLTF loading (GLTFast) is not compatible with WebGL due to Task usage
#if UNITY_WEBGL

using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.RealmNavigation;
using DCL.ResourcesUnloading;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.Systems;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.Visibility.Systems;
using System.Collections.Generic;
using ECS.Unity.GltfNodeModifiers.Systems;
using System.Threading;

namespace DCL.PluginSystem.World
{
    /// <summary>
    /// WebGL-specific GLTF Container Plugin that only uses Asset Bundles for loading.
    /// Raw GLTF loading via GLTFast is not supported on WebGL due to Task compatibility issues.
    /// </summary>
    public class GltfContainerPluginWebGL : IDCLWorldPluginWithoutSettings
    {
        static GltfContainerPluginWebGL()
        {
            EntityEventBuffer<GltfContainerComponent>.Register(1000);
        }

        private readonly GltfContainerAssetsCache assetsCache;
        private readonly IPerformanceBudget frameTimeBudget;
        private readonly MemoryBudget memoryBudget;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly ILoadingStatus loadingStatus;

        public GltfContainerPluginWebGL(
            IPerformanceBudget frameTimeBudget,
            MemoryBudget memoryBudget,
            CacheCleaner cacheCleaner,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            ILoadingStatus loadingStatus,
            IGltfContainerAssetsCache assetsCache)
        {
            this.frameTimeBudget = frameTimeBudget;
            this.memoryBudget = memoryBudget;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.loadingStatus = loadingStatus;
            this.assetsCache = (GltfContainerAssetsCache)assetsCache;

            cacheCleaner.Register(assetsCache);
        }

        public void Dispose()
        {
            assetsCache.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<GltfContainerComponent>();

            // Asset loading - ONLY Asset Bundle path for WebGL
            // LocalSceneDevelopment = false and UseRemoveAssetBundles = true forces Asset Bundle path
            PrepareGltfAssetLoadingSystem.InjectToWorld(
                ref builder,
                assetsCache,
                new PrepareGltfAssetLoadingSystem.Options
                {
                    LocalSceneDevelopment = false,
                    UseRemoveAssetBundles = true,
                    PreviewingBuilderCollection = false
                });

            // Create asset from loaded Asset Bundle (skip CreateGltfAssetFromRawGltfSystem - not compatible with WebGL)
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, frameTimeBudget, memoryBudget);

            // GLTF Node Modifier Systems
            SetupGltfNodeModifierSystem.InjectToWorld(ref builder);
            UpdateGltfNodeModifierSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(CleanupGltfNodeModifierSystem.InjectToWorld(ref builder, buffer));

            // GLTF Container Systems
            LoadGltfContainerSystem.InjectToWorld(ref builder, buffer, sharedDependencies.SceneData, sharedDependencies.EntityCollidersSceneCache);
            FinalizeGltfContainerLoadingSystem.InjectToWorld(ref builder, persistentEntities.SceneRoot, frameTimeBudget,
                sharedDependencies.EntityCollidersSceneCache, sharedDependencies.SceneData, buffer);

            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache, buffer, sharedDependencies.EcsToCRDTWriter);
            WriteGltfContainerLoadingStateSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, buffer);
            GltfContainerVisibilitySystem.InjectToWorld(ref builder, buffer);
            finalizeWorldSystems.Add(CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache, sharedDependencies.ScenePartition));

            // Scene readiness reporting
            GatherGltfAssetsSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, sharedDependencies.SceneData,
                buffer, sharedDependencies.SceneStateProvider, memoryBudget, loadingStatus,
                persistentEntities.SceneContainer);
        }

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}

#endif
