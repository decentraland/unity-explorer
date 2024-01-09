using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;

namespace ECS.Unity.AvatarShape.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class AvatarShapeLoaderSystem : BaseUnityLoopSystem
    {
        private WorldProxy globalWorld;

        internal AvatarShapeLoaderSystem(World world, WorldProxy globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            InstantiateAvatarShapeQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void InstantiateAvatarShape(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partitionComponent, ref TransformComponent transformComponent)
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
    }
}
