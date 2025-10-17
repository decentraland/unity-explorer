using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MediaStream;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using DCL.SDKComponents.NFTShape.Renderer;
using DCL.SDKComponents.NFTShape.Renderer.Factory;
using DCL.SDKComponents.NFTShape.System;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
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
        private readonly IFramePrefabs framePrefabs;
        private readonly IDiskCache<TextureData> diskCache;
        private readonly MediaFactoryBuilder mediaFactory;

        /// <summary>
        ///     We redirect to <see cref="TexturesCache{TIntention}" /> for plain images and no-cache for NFTs themselves, videos do not go through the cache
        /// </summary>
        private readonly IStreamableCache<TextureData, GetNFTShapeIntention> cache = new NoCache<TextureData, GetNFTShapeIntention>(true, true);

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
            IDiskCache<TextureData> diskCache,
            MediaFactoryBuilder mediaFactory) : this(
            decentralandUrlsSource,
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            new FramesPool(NewFramePrefabs(assetsProvisioner, out var framePrefabs), componentPoolsRegistry),
            framePrefabs,
            webRequestController,
            diskCache,
            mediaFactory) { }

        public NFTShapePlugin(
            IDecentralandUrlsSource decentralandUrlsSource,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IFramesPool framesPool,
            IFramePrefabs framePrefabs,
            IWebRequestController webRequestController,
            IDiskCache<TextureData> diskCache,
            MediaFactoryBuilder mediaFactory) : this(
            decentralandUrlsSource,
            new PoolNFTShapeRendererFactory(componentPoolsRegistry, framesPool),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            webRequestController,
            framePrefabs,
            diskCache,
            mediaFactory) { }

        public NFTShapePlugin(
            IDecentralandUrlsSource decentralandUrlsSource,
            INFTShapeRendererFactory nftShapeRendererFactory,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IComponentPoolsRegistry componentPoolsRegistry,
            IWebRequestController webRequestController,
            IFramePrefabs framePrefabs,
            IDiskCache<TextureData> diskCache,
            MediaFactoryBuilder mediaFactory)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;
            this.framePrefabs = framePrefabs;
            this.diskCache = diskCache;
            this.mediaFactory = mediaFactory;

            // cacheCleaner.Register(cache);
        }

        public void Dispose()
        {
            cache.Dispose();
        }

        public UniTask InitializeAsync(NFTShapePluginSettings settings, CancellationToken ct)
        {
            return framePrefabs.InitializeAsync(
                settings.Settings.FramePrefabs(),
                settings.Settings.DefaultFrame(),
                ct
            );
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<NftShapeRendererComponent>();

            bool isKtxEnabled = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.KTX2_CONVERSION);

            LoadNFTShapeSystem.InjectToWorld(ref builder, cache, webRequestController, diskCache, isKtxEnabled, mediaFactory.CreateForScene(builder.World, sharedDependencies), decentralandUrlsSource);
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
    }
}
