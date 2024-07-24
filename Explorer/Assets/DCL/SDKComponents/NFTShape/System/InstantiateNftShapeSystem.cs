using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Renderer.Factory;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class InstantiateNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly INFTShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IReadOnlyFramePrefabs prefabs;

        private readonly EntityEventBuffer<NftShapeRendererComponent> changedNftShapes;

        public InstantiateNftShapeSystem(
            World world,
            INFTShapeRendererFactory nftShapeRendererFactory,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IReadOnlyFramePrefabs prefabs,
            EntityEventBuffer<NftShapeRendererComponent> changedNftShapes) : base(world)
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.prefabs = prefabs;
            this.changedNftShapes = changedNftShapes;
        }

        protected override void Update(float t)
        {
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(NftShapeRendererComponent), typeof(MaterialComponent))]
        private void InstantiateRemaining(Entity entity, in TransformComponent transform, in PBNftShape nftShape)
        {
            if (prefabs.IsInitialized == false || instantiationFrameTimeBudgetProvider.TrySpendBudget() == false)
                return;

            var component = NewNftShapeRendererComponent(transform, nftShape);

            World!.Add(entity, component);
            changedNftShapes.Add(entity, component);
        }

        private NftShapeRendererComponent NewNftShapeRendererComponent(in TransformComponent transform, in PBNftShape nftShape)
        {
            var renderer = nftShapeRendererFactory.New(transform.Transform);
            renderer.Apply(nftShape);

            return new NftShapeRendererComponent(renderer);
        }
    }
}
