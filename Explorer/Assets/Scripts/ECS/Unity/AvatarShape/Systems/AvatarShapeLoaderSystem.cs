using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
    public partial class AvatarShapeLoaderSystem : BaseUnityLoopSystem
    {
        private World world;
        private WorldProxy globalWorld;

        internal AvatarShapeLoaderSystem(World world, WorldProxy globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
            this.world = world;
        }

        protected override void Update(float t)
        {
            InstantiateAvatarShapeQuery(world);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void InstantiateAvatarShape(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partitionComponent, ref TransformComponent transformComponent)
        {
            Entity globalEntity = globalWorld.Create();
            pbAvatarShape.IsDirty = true;
            globalWorld.Add(globalEntity, pbAvatarShape);
            globalWorld.Add(globalEntity, partitionComponent);
            globalWorld.Add(globalEntity, transformComponent);

            var satelliteComponent = new AvatarShapeComponent();
            world.Add(entity, satelliteComponent);
        }
    }
}
