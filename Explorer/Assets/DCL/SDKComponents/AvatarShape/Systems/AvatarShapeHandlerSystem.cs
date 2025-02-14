using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.Emotes;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using Utility.Arch;

namespace ECS.Unity.AvatarShape.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class AvatarShapeHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;

        public AvatarShapeHandlerSystem(World world, World globalWorld) : base(world)
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
        [None(typeof(SDKAvatarShapeComponent), typeof(DeleteEntityIntention))]
        private void LoadAvatarShape(Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partitionComponent, ref TransformComponent transformComponent)
        {
            var globalWorldEntity = globalWorld.Create(
                pbAvatarShape, partitionComponent,
                new CharacterTransform(transformComponent.Transform),
                new CharacterInterpolationMovementComponent(transformComponent.Transform.position, transformComponent.Transform.position, transformComponent.Transform.rotation),
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent());
            World.Add(entity, new SDKAvatarShapeComponent(globalWorldEntity));

            if (!string.IsNullOrEmpty(pbAvatarShape.ExpressionTriggerId))
                globalWorld.Add(globalWorldEntity, new CharacterEmoteIntent() { EmoteId = pbAvatarShape.ExpressionTriggerId });
        }

        [Query]
        private void UpdateAvatarShape(ref PBAvatarShape pbAvatarShape, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            globalWorld.Set(sdkAvatarShapeComponent.globalWorldEntity, pbAvatarShape);

            if (!string.IsNullOrEmpty(pbAvatarShape.ExpressionTriggerId))
                globalWorld.AddOrSet(sdkAvatarShapeComponent.globalWorldEntity, new CharacterEmoteIntent() { EmoteId = pbAvatarShape.ExpressionTriggerId });
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            // If the component is removed at scene-world, the global-world representation should disappear entirely
            MarkGlobalWorldEntityForDeletion(sdkAvatarShapeComponent.globalWorldEntity);

            World.Remove<SDKAvatarShapeComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(Entity entity, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            MarkGlobalWorldEntityForDeletion(sdkAvatarShapeComponent.globalWorldEntity);
            World.Remove<SDKAvatarShapeComponent>(entity);
        }

        [Query]
        public void FinalizeComponents(ref SDKAvatarShapeComponent sdkAvatarShapeComponent) =>
            MarkGlobalWorldEntityForDeletion(sdkAvatarShapeComponent.globalWorldEntity);

        public void FinalizeComponents(in Query query) =>
            FinalizeComponentsQuery(World);

        public void MarkGlobalWorldEntityForDeletion(Entity globalEntity)
        {
            // Has to be removed, otherwise scene loading may break after teleportation (no error anywhere to know why)
            globalWorld.Remove<CharacterTransform>(globalEntity);

            // Has to be deferred because many times it happens that the entity is marked for deletion AFTER the
            // AvatarCleanUpSystem.Update() and BEFORE the DestroyEntitiesSystem.Update(), probably has to do with
            // non-synchronicity between global and scene ECS worlds. AvatarCleanUpSystem resets the DeferDeletion.
            globalWorld.Add(globalEntity, new DeleteEntityIntention() { DeferDeletion = true });
        }
    }
}
