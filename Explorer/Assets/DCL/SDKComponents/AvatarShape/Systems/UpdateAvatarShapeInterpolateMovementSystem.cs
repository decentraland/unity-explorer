using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Transforms.Systems;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.AvatarShape.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class UpdateAvatarShapeInterpolateMovementSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly Vector2Int sceneBaseParcel;

        private UpdateAvatarShapeInterpolateMovementSystem(World world, World globalWorld, Vector2Int sceneBaseParcel) : base(world)
        {
            this.globalWorld = globalWorld;
            this.sceneBaseParcel = sceneBaseParcel;
        }

        protected override void Update(float t)
        {
            UpdateAvatarInterpolationMovementQuery(World);
            UpdateAvatarWithTweenInterpolationMovementQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void UpdateAvatarInterpolationMovement(in SDKAvatarShapeComponent sdkAvatarShapeComponent, in SDKTransform sdkTransform)
        {
            if (!sdkTransform.IsDirty)
                return;

            UpdateInterpolationMovement(in sdkAvatarShapeComponent, in sdkTransform, false, false);
        }

        [Query]
        private void UpdateAvatarWithTweenInterpolationMovement(
            in SDKAvatarShapeComponent sdkAvatarShapeComponent,
            in SDKTransform sdkTransform,
            in SDKTweenComponent sdkTweenComponent)
        {
            if ((sdkTweenComponent.TweenMode != PBTween.ModeOneofCase.Move && sdkTweenComponent.TweenMode != PBTween.ModeOneofCase.Rotate) ||
                sdkTweenComponent.TweenStateStatus != TweenStateStatus.TsActive)
                return;

            UpdateInterpolationMovement(
                in sdkAvatarShapeComponent,
                in sdkTransform,
                sdkTweenComponent.TweenMode == PBTween.ModeOneofCase.Move,
                sdkTweenComponent.TweenMode == PBTween.ModeOneofCase.Rotate);
        }

        private void UpdateInterpolationMovement(
            in SDKAvatarShapeComponent sdkAvatarShapeComponent,
            in SDKTransform sdkTransform,
            bool isPositionManagedByTween,
            bool isRotationManagedByTween)
        {
            ref CharacterInterpolationMovementComponent characterInterpolationMovementComponent = ref globalWorld.TryGetRef<CharacterInterpolationMovementComponent>(
                sdkAvatarShapeComponent.globalWorldEntity,
                out bool hasCharacterInterpolationMovement);

            if (!hasCharacterInterpolationMovement)
                return;

            characterInterpolationMovementComponent.TargetPosition = sdkTransform.Position.Value.FromSceneRelativeToGlobalPosition(sceneBaseParcel);
            characterInterpolationMovementComponent.TargetRotation = sdkTransform.Rotation.Value;
            characterInterpolationMovementComponent.IsPositionManagedByTween = isPositionManagedByTween;
            characterInterpolationMovementComponent.IsRotationManagedByTween = isRotationManagedByTween;
        }
    }
}
