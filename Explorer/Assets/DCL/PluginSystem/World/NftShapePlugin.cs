using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frame;
using DCL.SDKComponents.NftShape.Frames.Pool;
using DCL.SDKComponents.NftShape.Renderer;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using DCL.SDKComponents.NftShape.System;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.NftShapes;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.PluginSystem.World
{
    public class NftShapePlugin : IDCLWorldPlugin
    {
        private readonly INftShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IStreamableCache<Texture2D, GetNftShapeIntention> cache = new NftShapeCache();

        public NftShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry, IPluginSettingsContainer settingsContainer, IWebRequestController webRequestController) : this(
            new PoolNftShapeRendererFactory(componentPoolsRegistry, new FramesPool(settingsContainer.GetSettings<NftShapePluginSettings>().Settings)),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            webRequestController
        ) { }

        public NftShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry, IFramesPool framesPool, IWebRequestController webRequestController) : this(
            new PoolNftShapeRendererFactory(componentPoolsRegistry, framesPool),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            webRequestController
        ) { }

        public NftShapePlugin(INftShapeRendererFactory nftShapeRendererFactory, IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry, IWebRequestController webRequestController)
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.webRequestController = webRequestController;
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            Inject(ref builder, sharedDependencies.SceneData);
            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<INftShapeRenderer, NftShapeRendererComponent>(ref builder, componentPoolsRegistry);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) =>
            Inject(ref builder, dependencies.SceneData);

        private void Inject(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, ISceneData sceneData)
        {
            LoadNftShapeSystem.InjectToWorld(ref builder, cache, webRequestController, new MutexSync());
            InstantiateNftShapeSystem.InjectToWorld(ref builder, nftShapeRendererFactory, instantiationFrameTimeBudgetProvider, sceneData: sceneData);
            ApplyMaterialNftShapeSystem.InjectToWorld(ref builder, sceneData);
            VisibilityNftShapeSystem.InjectToWorld(ref builder);
        }
    }
}
