using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
using DCL.WebRequests.ArgsFactory;
using DCL.WebRequests.WebContentSizes;
using DCL.WebRequests.WebContentSizes.Sizes.Lazy;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class NFTShapePlugin : IDCLWorldPlugin<NFTShapePluginSettings>
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly INFTShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IGetTextureArgsFactory getTextureArgsFactory;
        private readonly IFramePrefabs framePrefabs;
        private readonly ILazyMaxSize lazyMaxSize;
        private readonly IStreamableCache<Texture2DData, GetNFTShapeIntention> cache = new NftShapeCache();

        static NFTShapePlugin()
        {
            EntityEventBuffer<NftShapeRendererComponent>.Register(100);
        }

        public NFTShapePlugin(
            IDecentralandUrlsSource decentralandUrlsSource,
            IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            CacheCleaner cacheCleaner
        ) : this(
            decentralandUrlsSource,
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            new FramesPool(NewFramePrefabs(assetsProvisioner, out var framePrefabs)),
            framePrefabs,
            webRequestController,
            getTextureArgsFactory,
            cacheCleaner,
            new IWebContentSizes.Default(LazyMaxSize(out var lazyMaxSize)),
            lazyMaxSize
        ) { }

        public NFTShapePlugin(
            IDecentralandUrlsSource decentralandUrlsSource,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IFramesPool framesPool,
            IFramePrefabs framePrefabs,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            CacheCleaner cacheCleaner,
            IWebContentSizes webContentSizes,
            ILazyMaxSize lazyMaxSize
        ) : this(
            decentralandUrlsSource,
            new PoolNFTShapeRendererFactory(componentPoolsRegistry, framesPool),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            webRequestController,
            getTextureArgsFactory,
            cacheCleaner,
            framePrefabs,
            lazyMaxSize
        ) { }

        public NFTShapePlugin(
            IDecentralandUrlsSource decentralandUrlsSource,
            INFTShapeRendererFactory nftShapeRendererFactory,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            CacheCleaner cacheCleaner,
            IFramePrefabs framePrefabs,
            ILazyMaxSize lazyMaxSize
        )
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
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

            LoadNFTShapeSystem.InjectToWorld(ref builder, cache, webRequestController, getTextureArgsFactory);
            LoadCycleNftShapeSystem.InjectToWorld(ref builder, new BasedURNSource(decentralandUrlsSource));
            InstantiateNftShapeSystem.InjectToWorld(ref builder, nftShapeRendererFactory, instantiationFrameTimeBudgetProvider, framePrefabs, buffer);
            VisibilityNftShapeSystem.InjectToWorld(ref builder, buffer);

            ResetDirtyFlagSystem<PBNftShape>.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(CleanUpNftShapeSystem.InjectToWorld(ref builder));
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
