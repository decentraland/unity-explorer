using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Transforms.Systems;

namespace DCL.SDKComponents.AvatarShape.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class UpdateAvatarShapeTargetPositionSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;

        private UpdateAvatarShapeTargetPositionSystem(World world, World globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            UpdateAvatarTargetPositionQuery(World);
            UpdateAvatarWithTweenTargetPositionQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void UpdateAvatarTargetPosition(ref SDKAvatarShapeComponent sdkAvatarShapeComponent, ref SDKTransform sdkTransform)
        {
            if (!sdkTransform.IsDirty)
                return;

            UpdateTargetPosition(ref sdkAvatarShapeComponent, ref sdkTransform, false);
        }

        [Query]
        private void UpdateAvatarWithTweenTargetPosition(
            ref SDKAvatarShapeComponent sdkAvatarShapeComponent,
            ref SDKTransform sdkTransform,
            ref SDKTweenComponent sdkTweenComponent)
        {
            if (sdkTweenComponent.TweenMode != PBTween.ModeOneofCase.Move || sdkTweenComponent.TweenStateStatus != TweenStateStatus.TsActive)
                return;

            UpdateTargetPosition(ref sdkAvatarShapeComponent, ref sdkTransform, true);
        }

        private void UpdateTargetPosition(
            ref SDKAvatarShapeComponent sdkAvatarShapeComponent,
            ref SDKTransform sdkTransform,
            bool isManagedByTween)
        {
            ref CharacterTargetPositionComponent characterTargetPositionComponent = ref globalWorld.TryGetRef<CharacterTargetPositionComponent>(
                sdkAvatarShapeComponent.globalWorldEntity,
                out bool hasCharacterTargetPosition);

            if (!hasCharacterTargetPosition)
                return;

            characterTargetPositionComponent.TargetPosition = sdkTransform.Position;
            characterTargetPositionComponent.FinalRotation = sdkTransform.Rotation;
            characterTargetPositionComponent.IsManagedByTween = isManagedByTween;
        }
    }
}
