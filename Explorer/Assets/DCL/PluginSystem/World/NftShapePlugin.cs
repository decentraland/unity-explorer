using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frame;
using DCL.SDKComponents.NftShape.Frames.FramePrefabs;
using DCL.SDKComponents.NftShape.Frames.Pool;
using DCL.SDKComponents.NftShape.Renderer;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using DCL.SDKComponents.NftShape.System;
using DCL.WebRequests;
using DCL.WebRequests.WebContentSizes;
using DCL.WebRequests.WebContentSizes.Sizes.Lazy;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.NftShapes;
using ECS.StreamableLoading.NftShapes.Urns;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.PluginSystem.World
{
    public class NftShapePlugin : IDCLWorldPlugin<NftShapePluginSettings>
    {
        private readonly INftShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IWebContentSizes webContentSizes;
        private readonly IFramePrefabs framePrefabs;
        private readonly ILazyMaxSize lazyMaxSize;
        private readonly IStreamableCache<Texture2D, GetNftShapeIntention> cache = new NftShapeCache();

        public NftShapePlugin(
            IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner
        ) : this(
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            new FramesPool(NewFramePrefabs(assetsProvisioner, out var framePrefabs)),
            framePrefabs,
            webRequestController,
            cacheCleaner,
            new IWebContentSizes.Default(LazyMaxSize(out var lazyMaxSize)),
            lazyMaxSize
        ) { }

        public NftShapePlugin(
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IFramesPool framesPool,
            IFramePrefabs framePrefabs,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            IWebContentSizes webContentSizes,
            ILazyMaxSize lazyMaxSize
        ) : this(
            new PoolNftShapeRendererFactory(componentPoolsRegistry, framesPool),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            webRequestController,
            cacheCleaner,
            webContentSizes,
            framePrefabs,
            lazyMaxSize
        ) { }

        public NftShapePlugin(
            INftShapeRendererFactory nftShapeRendererFactory,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            IWebContentSizes webContentSizes,
            IFramePrefabs framePrefabs,
            ILazyMaxSize lazyMaxSize
        )
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;
            this.webContentSizes = webContentSizes;
            this.framePrefabs = framePrefabs;
            this.lazyMaxSize = lazyMaxSize;
            cacheCleaner.Register(cache);
        }

        public void Dispose()
        {
            cache.Dispose();
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public UniTask InitializeAsync(NftShapePluginSettings settings, CancellationToken ct)
        {
            lazyMaxSize.Initialize(settings.MaxSizeOfNftForDownload);

            return framePrefabs.Initialize(
                settings.Settings.FramePrefabs(),
                settings.Settings.DefaultFrame(),
                ct
            );
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            Inject(ref builder, sharedDependencies.SceneData);
            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<INftShapeRenderer, NftShapeRendererComponent>(ref builder, componentPoolsRegistry);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) =>
            Inject(ref builder, dependencies.SceneData);

        private void Inject(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, ISceneData sceneData)
        {
            LoadNftShapeSystem.InjectToWorld(ref builder, cache, webRequestController, new MutexSync(), webContentSizes);
            LoadCycleNftShapeSystem.InjectToWorld(ref builder, new BasedUrnSource());
            InstantiateNftShapeSystem.InjectToWorld(ref builder, nftShapeRendererFactory, instantiationFrameTimeBudgetProvider, sceneData: sceneData);
            VisibilityNftShapeSystem.InjectToWorld(ref builder);
        }

        private static IFramePrefabs NewFramePrefabs(IAssetsProvisioner assetsProvisioner, out IFramePrefabs framePrefabs)
        {
            return framePrefabs = new AssetProvisionerFramePrefabs(assetsProvisioner);
        }

        private static ILazyMaxSize LazyMaxSize(out ILazyMaxSize lazyMaxSize)
        {
            return lazyMaxSize = new LazyMaxSize();
        }
    }
}
