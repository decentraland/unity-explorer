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
using DCL.ResourcesUnloading;
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
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

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
        private readonly ExtendedObjectPool<Texture2D> videoTexturePool;
        private readonly NftShapeCache nftShapeCache;
        private readonly TexturesCache<GetNFTImageIntention> imageCache;
        private readonly TexturesCache<GetNFTVideoIntention> videoCache;

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
            CacheCleaner cacheCleaner,
            ExtendedObjectPool<Texture2D> videoTexturePool)
        {
            this.framePrefabs = new AssetProvisionerFramePrefabs(assetsProvisioner);
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.nftShapeRendererFactory = new PoolNFTShapeRendererFactory(componentPoolsRegistry,
                new FramesPool(framePrefabs, componentPoolsRegistry));
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;
            this.videoTexturePool = videoTexturePool;
            // See https://github.com/decentraland/unity-explorer/issues/5611
            // videos & images requires different caches
            imageCache = new TexturesCache<GetNFTImageIntention>();
            videoCache = new TexturesCache<GetNFTVideoIntention>();
            nftShapeCache = new NftShapeCache(imageCache, videoCache);
            cacheCleaner.Register(nftShapeCache);
        }

        public void Dispose()
        {
            nftShapeCache.Dispose();
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

            LoadNFTTypeSystem.InjectToWorld(ref builder, NoCache<NftTypeResult, GetNFTTypeIntention>.INSTANCE, webRequestController, isKtxEnabled, decentralandUrlsSource);
            LoadNFTImageSystem.InjectToWorld(ref builder, imageCache, webRequestController, isKtxEnabled);
            LoadNFTVideoSystem.InjectToWorld(ref builder, videoCache, videoTexturePool);
            LoadCycleNftShapeSystem.InjectToWorld(ref builder, new BasedURNSource(decentralandUrlsSource));
            InstantiateNftShapeSystem.InjectToWorld(ref builder, nftShapeRendererFactory, instantiationFrameTimeBudgetProvider, framePrefabs, buffer);
            VisibilityNftShapeSystem.InjectToWorld(ref builder, buffer);

            ResetDirtyFlagSystem<PBNftShape>.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(CleanUpNftShapeSystem.InjectToWorld(ref builder));
            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<INftShapeRenderer, NftShapeRendererComponent>(ref builder, componentPoolsRegistry);
        }
    }
}
