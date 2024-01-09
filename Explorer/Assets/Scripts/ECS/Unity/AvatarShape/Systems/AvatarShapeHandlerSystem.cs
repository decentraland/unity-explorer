using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;

namespace ECS.Unity.AvatarShape.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class AvatarShapeHandlerSystem : BaseUnityLoopSystem
    {
        private WorldProxy globalWorld;

        internal AvatarShapeHandlerSystem(World world, WorldProxy globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            LoadAvatarShapeQuery(World);
            UpdateAvatarShapeQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void LoadAvatarShape(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partitionComponent, ref TransformComponent transformComponent)
        {
            globalWorld.Add(entity, pbAvatarShape);
            globalWorld.Add(entity, partitionComponent);
            globalWorld.Add(entity, transformComponent);

            World.Add(entity, new AvatarShapeComponent());
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        private void UpdateAvatarShape(in Entity entity, ref PBAvatarShape pbAvatarShape)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            globalWorld.Add(entity, pbAvatarShape);
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity)
        {
            World.Remove<AvatarShapeComponent>(entity);

            // If the component is removed at scene-world, the global-world representation should disappear entirely
            globalWorld.Add(entity, new DeleteEntityIntention());
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity)
        {
            World.Remove<AvatarShapeComponent>(entity);

            globalWorld.Add(entity, new DeleteEntityIntention());
        }
    }
}
