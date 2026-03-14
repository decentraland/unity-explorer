using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    public partial class RemoteHandPointAtSystem : BaseUnityLoopSystem
    {
        private readonly ICharacterControllerSettings localSettings;

        private RemoteHandPointAtSystem(
            World world,
            ICharacterControllerSettings localSettings) : base(world)
        {
            this.localSettings = localSettings;
        }

        protected override void Update(float t)
        {
            ApplyRemotePointAtIKQuery(World, t);
        }

        [Query]
        [All(typeof(RemotePlayerMovementComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyRemotePointAtIK(
            [Data] float dt,
            ref HandPointAtComponent pointAt,
            ref AvatarBase avatarBase
        )
        {
            HandPointAtHelper.ApplyAnimationWeight(ref pointAt, ref avatarBase, in localSettings, dt);
            avatarBase.RightHandIK.weight = pointAt.AnimationWeight;
            avatarBase.HandsIKRig.weight = pointAt.AnimationWeight;

            if (!pointAt.IsPointing)
            {
                pointAt.RotationAnimationWeight = 0f;
                pointAt.PreviousLookDirection = Vector3.zero;
                HandPointAtHelper.SetPlayerRotationAnimation(ref avatarBase, pointAt.RotationAnimationWeight, false, false);
                return;
            }

            Vector3 shoulderPos = avatarBase.RightShoulderAnchorPoint.position;

            Vector3 directionToTarget = HandPointAtHelper.ClampElevation(
                (pointAt.WorldHitPoint - shoulderPos).normalized,
                localSettings.PointAtRotationVerticalUpThreshold,
                localSettings.PointAtRotationVerticalDownThreshold);

            if (pointAt.PreviousLookDirection != Vector3.zero)
            {
                Vector3 cross = Vector3.Cross(avatarBase.transform.forward, pointAt.PreviousLookDirection);
                float dot  = Vector3.Dot(avatarBase.transform.forward, pointAt.PreviousLookDirection);
                HandPointAtHelper.PlayerRotationAnimation(localSettings, ref avatarBase, ref pointAt, !Mathf.Approximately(dot, 1f), dt, cross.y);
            }

            pointAt.IsDragging = false;
            var rotationInfo = HandPointAtHelper.CalculateAvatarRotation(avatarBase, localSettings, avatarBase.transform.forward, directionToTarget);

            HandPointAtHelper.ApplyHandIK(ref pointAt, ref avatarBase, in localSettings, dt, directionToTarget, shoulderPos, rotationInfo, true);

            pointAt.PreviousLookDirection = avatarBase.transform.forward;
        }
    }
}
