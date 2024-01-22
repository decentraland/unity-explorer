using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.Unity.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;

namespace DCL.SDKComponents.NftShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class InstantiateNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly INftShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly ISceneData sceneData;

        public InstantiateNftShapeSystem(
            World world,
            INftShapeRendererFactory nftShapeRendererFactory,
            IPerformanceBudget? instantiationFrameTimeBudgetProvider = null,
            ISceneData? sceneData = null
        ) : base(world)
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider ?? new FrameTimeCapBudget();
            this.sceneData = sceneData ?? new ISceneData.Fake();
        }

        protected override void Update(float t)
        {
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(NftShapeRendererComponent), typeof(MaterialComponent))]
        private void InstantiateRemaining(in Entity entity, in TransformComponent transform, in PBNftShape nftShape, ref PartitionComponent partitionComponent)
        {
            if (instantiationFrameTimeBudgetProvider.TrySpendBudget() == false)
                return;

            World.Add(entity, NewNftShapeRendererComponent(transform, nftShape));
        }

        private NftShapeRendererComponent NewNftShapeRendererComponent(in TransformComponent transform, in PBNftShape nftShape)
        {
            var renderer = nftShapeRendererFactory.New(transform.Transform);
            renderer.Apply(nftShape);
            return new NftShapeRendererComponent(renderer);
        }
    }
}
