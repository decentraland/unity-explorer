using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using DCL.SDKComponents.NFTShape.Renderer;
using DCL.SDKComponents.NFTShape.Renderer.Factory;
using DCL.SDKComponents.NFTShape.System;
using DCL.WebRequests;
using DCL.WebRequests.WebContentSizes;
using DCL.WebRequests.WebContentSizes.Sizes.Lazy;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class NFTShapePlugin : IDCLWorldPlugin<NFTShapePluginSettings>
    {
        private readonly INFTShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IWebContentSizes webContentSizes;
        private readonly IFramePrefabs framePrefabs;
        private readonly ILazyMaxSize lazyMaxSize;
        private readonly IStreamableCache<Texture2D, GetNFTShapeIntention> cache = new NftShapeCache();

        static NFTShapePlugin()
        {
            EntityEventBuffer<NftShapeRendererComponent>.Register(100);
        }

        public NFTShapePlugin(
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

        public NFTShapePlugin(
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IFramesPool framesPool,
            IFramePrefabs framePrefabs,
            IWebRequestController webRequestController,
            CacheCleaner cacheCleaner,
            IWebContentSizes webContentSizes,
            ILazyMaxSize lazyMaxSize
        ) : this(
            new PoolNFTShapeRendererFactory(componentPoolsRegistry, framesPool),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            webRequestController,
            cacheCleaner,
            webContentSizes,
            framePrefabs,
            lazyMaxSize
        ) { }

        public NFTShapePlugin(
            INFTShapeRendererFactory nftShapeRendererFactory,
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


        public UniTask InitializeAsync(NFTShapePluginSettings settings, CancellationToken ct)
        {
            lazyMaxSize.Initialize(settings.MaxSizeOfNftForDownload);

            return framePrefabs.InitializeAsync(
                settings.Settings.FramePrefabs(),
                settings.Settings.DefaultFrame(),
                ct
            );
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<NftShapeRendererComponent>();

            LoadNFTShapeSystem.InjectToWorld(ref builder, cache, webRequestController, webContentSizes);
            LoadCycleNftShapeSystem.InjectToWorld(ref builder, new BasedURNSource());
            InstantiateNftShapeSystem.InjectToWorld(ref builder, nftShapeRendererFactory, instantiationFrameTimeBudgetProvider, framePrefabs, buffer);
            VisibilityNftShapeSystem.InjectToWorld(ref builder, buffer);

            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<INftShapeRenderer, NftShapeRendererComponent>(ref builder, componentPoolsRegistry);
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
