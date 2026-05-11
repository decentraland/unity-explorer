using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.RealmNavigation;
using DCL.ResourcesUnloading;
using DCL.Utility;
using DCL.WebRequests;
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
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.GLTF.DownloadProvider;
using ECS.Unity.GltfNodeModifiers.Systems;
using Global.AppArgs;
using System.Threading;
using UnityEngine;

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
        private readonly IAppArgs appArgs;
        private readonly Transform poolsRoot;

        // Process-wide raw-GLTF cache. Combined with per-consumer Root cloning in
        // CreateGltfAssetFromRawGltfSystem, multiple entities referencing the same hash share
        // a single GltfImport and its underlying Mesh / Material / Texture allocations.
        private readonly GltfLoadCache gltfLoadCache = new ();

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies globalDeps,
            CacheCleaner cacheCleaner,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            ILaunchMode launchMode,
            bool useRemoteAssetBundles,
            IWebRequestController webRequestController,
            ILoadingStatus loadingStatus,
            IGltfContainerAssetsCache assetsCache,
            IAppArgs appArgs,
            Transform poolsRoot)
        {
            this.globalDeps = globalDeps;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.launchMode = launchMode;
            this.useRemoteAssetBundles = useRemoteAssetBundles;
            this.webRequestController = webRequestController;
            this.loadingStatus = loadingStatus;
            this.assetsCache = (GltfContainerAssetsCache)assetsCache;
            this.appArgs = appArgs;
            this.poolsRoot = poolsRoot;

            cacheCleaner.Register(assetsCache);
            cacheCleaner.Register(gltfLoadCache);
        }

        public void Dispose()
        {
            assetsCache.Dispose();
            gltfLoadCache.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            bool localSceneDevelopment = launchMode.CurrentMode is LaunchMode.LocalSceneDevelopment;
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<GltfContainerComponent>();

            LoadGLTFSystem.InjectToWorld(
                ref builder,
                gltfLoadCache,
                webRequestController,
                false,
                false,
                localSceneDevelopment,
                new GltFastSceneDownloadStrategy(sharedDependencies.SceneData),
                poolsRoot);

            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(
                ref builder,
                assetsCache,
                sharedDependencies.SceneData,
                new PrepareGltfAssetLoadingSystem.Options
                {
                    LocalSceneDevelopment = localSceneDevelopment,
                    UseRemoteAssetBundles = useRemoteAssetBundles,
                    PreviewingBuilderCollection = appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS)
                });

            CreateGltfAssetFromRawGltfSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, globalDeps.MemoryBudget);
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, globalDeps.MemoryBudget);

            // GLTF Node Modifier Systems
            SetupGltfNodeModifierSystem.InjectToWorld(ref builder);
            UpdateGltfNodeModifierSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(CleanupGltfNodeModifierSystem.InjectToWorld(ref builder, buffer));

            // GLTF Container
            // Bridge GLTF cache keying to the AB layer without leaking AB symbols into LoadGltfContainerSystem:
            // when the scene's manifest has a deps digest for the hash, the key becomes "hash@digest", else the bare hash.
            var sceneData = sharedDependencies.SceneData;
            LoadGltfContainerSystem.InjectToWorld(ref builder, buffer, sceneData, sharedDependencies.EntityCollidersSceneCache,
                hash => sceneData.SceneEntityDefinition.assetBundleManifestVersion.ComposeCacheKey(hash));
            FinalizeGltfContainerLoadingSystem.InjectToWorld(ref builder, persistentEntities.SceneRoot, globalDeps.FrameTimeBudget,
                sharedDependencies.EntityCollidersSceneCache, sharedDependencies.SceneData, buffer);

            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache, buffer, sharedDependencies.EcsToCRDTWriter);
            WriteGltfContainerLoadingStateSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, buffer);
            GltfContainerVisibilitySystem.InjectToWorld(ref builder, buffer);
            finalizeWorldSystems.Add(CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache, sharedDependencies.ScenePartition));

            GatherGltfAssetsSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, sharedDependencies.SceneData,
                buffer, sharedDependencies.SceneStateProvider, globalDeps.MemoryBudget, loadingStatus,
                persistentEntities.SceneContainer);
        }

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
