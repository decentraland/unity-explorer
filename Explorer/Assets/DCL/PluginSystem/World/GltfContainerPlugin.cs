using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.GltfNode.Systems;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.Systems;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.Visibility.Systems;
using System.Collections.Generic;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.GLTF;

namespace DCL.PluginSystem.World
{
    public class GltfContainerPlugin : IDCLWorldPluginWithoutSettings
    {
        static GltfContainerPlugin()
        {
            EntityEventBuffer<GltfContainerComponent>.Register(1000);
        }

        private readonly GltfContainerAssetsCache assetsCache;
        private readonly ECSWorldSingletonSharedDependencies globalDeps;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly SceneAssetLock sceneAssetLock;
        private readonly bool localSceneDevelopment;
        private readonly bool useRemoteAssetBundles;
        private readonly ILoadingStatus loadingStatus;

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies globalDeps, CacheCleaner cacheCleaner, ISceneReadinessReportQueue sceneReadinessReportQueue, SceneAssetLock sceneAssetLock, IComponentPoolsRegistry poolsRegistry, bool localSceneDevelopment, bool useRemoteAssetBundles, ILoadingStatus loadingStatus)
        {
            this.globalDeps = globalDeps;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.sceneAssetLock = sceneAssetLock;
            this.localSceneDevelopment = localSceneDevelopment;
            this.useRemoteAssetBundles = useRemoteAssetBundles;
            this.loadingStatus = loadingStatus;
            assetsCache = new GltfContainerAssetsCache(poolsRegistry);

            cacheCleaner.Register(assetsCache);
        }

        public void Dispose()
        {
            assetsCache.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<GltfContainerComponent>();

            LoadGLTFSystem.InjectToWorld(ref builder, new NoCache<GLTFData, GetGLTFIntention>(false, false), sharedDependencies.SceneData);

            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(ref builder, assetsCache, localSceneDevelopment, useRemoteAssetBundles);

            if (localSceneDevelopment && !useRemoteAssetBundles)
                CreateGltfAssetFromRawGltfSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, globalDeps.MemoryBudget);
            else
                CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, globalDeps.MemoryBudget);

            // GLTF Container
            LoadGltfContainerSystem.InjectToWorld(ref builder, buffer, sharedDependencies.SceneData, sharedDependencies.EntityCollidersSceneCache);
            FinalizeGltfContainerLoadingSystem.InjectToWorld(ref builder, persistentEntities.SceneRoot, globalDeps.FrameTimeBudget,
                sharedDependencies.EntityCollidersSceneCache, sharedDependencies.SceneData, buffer);

            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache, buffer, sharedDependencies.EcsToCRDTWriter);
            WriteGltfContainerLoadingStateSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, buffer);
            GltfContainerVisibilitySystem.InjectToWorld(ref builder, buffer);

            GatherGltfAssetsSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, sharedDependencies.SceneData,
                buffer, sharedDependencies.SceneStateProvider, globalDeps.MemoryBudget, loadingStatus);

            // GltfNode
            GltfNodeSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap);
            // TODO: GltfNodeLoadingState


            ResetDirtyFlagSystem<PBGltfContainer>.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache));
        }
    }
}
