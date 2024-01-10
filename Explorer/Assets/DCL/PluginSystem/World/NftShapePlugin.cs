using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Renderer;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using DCL.SDKComponents.NftShape.System;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class NftShapePlugin : IDCLWorldPlugin
    {
        private readonly INftShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public NftShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry) : this(
            new PoolNftShapeRendererFactory(componentPoolsRegistry),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry
        ) { }

        public NftShapePlugin(INftShapeRendererFactory nftShapeRendererFactory, IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
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
            InstantiateNftShapeSystem.InjectToWorld(ref builder, nftShapeRendererFactory, instantiationFrameTimeBudgetProvider, sceneData: sceneData);
            VisibilityNftShapeSystem.InjectToWorld(ref builder);
        }
    }
}
