using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;

namespace ECS.Unity.AvatarShape.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class AvatarShapeHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ObjectProxy<World> globalWorldProxy;

        public AvatarShapeHandlerSystem(World world, ObjectProxy<World> globalWorldProxy) : base(world)
        {
            this.globalWorldProxy = globalWorldProxy;
        }

        protected override void Update(float t)
        {
            LoadAvatarShapeQuery(World);
            UpdateAvatarShapeQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(SDKAvatarShapeComponent))]
        private void LoadAvatarShape(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partitionComponent, ref TransformComponent transformComponent)
        {
            World.Add(entity, new SDKAvatarShapeComponent(globalWorldProxy.Object!.Create(pbAvatarShape, partitionComponent, transformComponent)));
        }

        [Query]
        private void UpdateAvatarShape(ref PBAvatarShape pbAvatarShape, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            globalWorldProxy.Object!.Set(sdkAvatarShapeComponent.globalWorldEntity, pbAvatarShape);
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            // If the component is removed at scene-world, the global-world representation should disappear entirely
            globalWorldProxy.Object!.Add(sdkAvatarShapeComponent.globalWorldEntity, new DeleteEntityIntention());

            World.Remove<SDKAvatarShapeComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            World.Remove<SDKAvatarShapeComponent>(entity);
            World.Remove<PBAvatarShape>(entity);
            globalWorldProxy.Object!.Add(sdkAvatarShapeComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        [Query]
        public void FinalizeComponents(ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            globalWorldProxy.Object!.Add(sdkAvatarShapeComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
