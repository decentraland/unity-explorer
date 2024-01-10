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
using System.Collections.Generic;

namespace ECS.Unity.AvatarShape.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class AvatarShapeHandlerSystem : BaseUnityLoopSystem
    {
        private WorldProxy globalWorld;
        private Dictionary<Entity, Entity> globalEntitiesMap = new Dictionary<Entity, Entity>();

        public AvatarShapeHandlerSystem(World world, WorldProxy globalWorld) : base(world)
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
            Entity globalWorldEntity = globalWorld.Create(pbAvatarShape, partitionComponent, transformComponent);

            World.Add(entity, new AvatarShapeComponent());

            globalEntitiesMap[entity] = globalWorldEntity;
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        private void UpdateAvatarShape(in Entity entity, ref PBAvatarShape pbAvatarShape)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            globalWorld.Add(globalEntitiesMap[entity], pbAvatarShape);
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity)
        {
            World.Remove<AvatarShapeComponent>(entity);

            // If the component is removed at scene-world, the global-world representation should disappear entirely
            RemoveGlobalEntity(entity);
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity)
        {
            World.Remove<AvatarShapeComponent>(entity);
            RemoveGlobalEntity(entity);
        }

        private void RemoveGlobalEntity(Entity sceneEntity)
        {
            globalWorld.Add(globalEntitiesMap[sceneEntity], new DeleteEntityIntention());
            globalEntitiesMap.Remove(sceneEntity);
        }
    }
}
