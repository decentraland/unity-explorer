using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using DCL.SDKComponents.NftShape.System;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class NftShapePlugin : IDCLWorldPlugin
    {
        private readonly INftShapeRendererFactory nftShapeRendererFactory;
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;

        public NftShapePlugin(IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry) : this(
            new PoolNftShapeRendererFactory(componentPoolsRegistry),
            instantiationFrameTimeBudgetProvider
        ) { }

        public NftShapePlugin(INftShapeRendererFactory nftShapeRendererFactory, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider)
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems) =>
            Inject(ref builder);

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) =>
            Inject(ref builder);

        private void Inject(ref ArchSystemsWorldBuilder<Arch.Core.World> builder)
        {
            InstantiateNftShapeSystem.InjectToWorld(ref builder, nftShapeRendererFactory, instantiationFrameTimeBudgetProvider);
            VisibilityNftShapeSystem.InjectToWorld(ref builder);
        }
    }
}
