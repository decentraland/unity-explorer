using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.RealmNavigation;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
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
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.GLTF;
using Global.Dynamic.LaunchModes;
using ECS.StreamableLoading.GLTF.DownloadProvider;

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
        private readonly ILaunchMode launchMode;
        private readonly bool useRemoteAssetBundles;
        private readonly IWebRequestController webRequestController;
        private readonly ILoadingStatus loadingStatus;

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies globalDeps, CacheCleaner cacheCleaner, ISceneReadinessReportQueue sceneReadinessReportQueue, IComponentPoolsRegistry poolsRegistry, ILaunchMode launchMode, bool useRemoteAssetBundles, IWebRequestController webRequestController, ILoadingStatus loadingStatus)
        {
            this.globalDeps = globalDeps;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.launchMode = launchMode;
            this.useRemoteAssetBundles = useRemoteAssetBundles;
            this.webRequestController = webRequestController;
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

            LoadGLTFSystem.InjectToWorld(
                ref builder,
                NoCache<GLTFData, GetGLTFIntention>.INSTANCE,
                webRequestController,
                false,
                false,
                new GltFastSceneDownloadStrategy(sharedDependencies.SceneData));

            bool localSceneDevelopment = launchMode.CurrentMode is LaunchMode.LocalSceneDevelopment;

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
                buffer, sharedDependencies.SceneStateProvider, globalDeps.MemoryBudget, loadingStatus,
                persistentEntities.SceneContainer);

            var cleanUpGltfContainerSystem =
                CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache);

            finalizeWorldSystems.Add(cleanUpGltfContainerSystem);
        }
    }
}
