using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Renderer.Factory;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [UpdateBefore(typeof(ParentingTransformSystem))]
    public partial class InstantiateTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly ITextShapeRendererFactory textShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;

        public InstantiateTextShapeSystem(World world, ITextShapeRendererFactory textShapeRendererFactory, IPerformanceBudget instantiationFrameTimeBudget) : base(world)
        {
            this.textShapeRendererFactory = textShapeRendererFactory;
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
        }

        public InstantiateTextShapeSystem(World world, ITextShapeRendererFactory textShapeRendererFactory) : this(
            world,
            textShapeRendererFactory,
            new FrameTimeCapBudget(
                33,
                new ProfilingProvider()
            )
        ) { }

        protected override void Update(float t)
        {
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(TextShapeRendererComponent))]
        private void InstantiateRemaining(in Entity entity, in TransformComponent transform, in PBTextShape textShape)
        {
            // if (instantiationFrameTimeBudget.TrySpendBudget() == false)
            //     return;

            var renderer = textShapeRendererFactory.New(transform.Transform);
            renderer.Apply(textShape);
            World.Add(entity, new TextShapeRendererComponent(renderer));
        }
    }
}
