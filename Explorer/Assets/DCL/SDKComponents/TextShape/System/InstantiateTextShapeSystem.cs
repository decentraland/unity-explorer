using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Renderer.Factory;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class InstantiateTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly ITextShapeRendererFactory textShapeRendererFactory;

        public InstantiateTextShapeSystem(World world, ITextShapeRendererFactory textShapeRendererFactory) : base(world)
        {
            this.textShapeRendererFactory = textShapeRendererFactory;
        }

        protected override void Update(float t)
        {
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(TextShapeRendererComponent))]
        private void InstantiateRemaining(in Entity entity, in TransformComponent transform, in PBTextShape textShape)
        {
            var renderer = textShapeRendererFactory.New(transform.Transform);
            renderer.Apply(textShape);
            World.Add(entity, new TextShapeRendererComponent(renderer));
        }
    }
}
