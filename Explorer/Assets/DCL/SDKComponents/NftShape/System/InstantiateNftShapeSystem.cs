using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;

namespace DCL.SDKComponents.NftShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class InstantiateNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly INftShapeRendererFactory nftShapeRendererFactory;
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;

        public InstantiateNftShapeSystem(World world, INftShapeRendererFactory nftShapeRendererFactory, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider) : base(world)
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
        }

        public InstantiateNftShapeSystem(World world, INftShapeRendererFactory nftShapeRendererFactory) : this(
            world,
            nftShapeRendererFactory,
            new FrameTimeCapBudgetProvider(
                33,
                new ProfilingProvider()
            )
        ) { }

        protected override void Update(float t)
        {
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(NftShapeRendererComponent))]
        private void InstantiateRemaining(in Entity entity, in TransformComponent transform, in PBNftShape nftShape)
        {
            if (instantiationFrameTimeBudgetProvider.TrySpendBudget() == false)
                return;

            var renderer = nftShapeRendererFactory.New(transform.Transform);
            renderer.Apply(nftShape);
            World.Add(entity, new NftShapeRendererComponent(renderer));
        }
    }
}
