using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    public partial class HandPointAtSystem : BaseUnityLoopSystem
    {
        private const float DRAG_THRESHOLD_SQR = 25f;
        private static readonly int RAYCAST_LAYER_MASK = PhysicsLayers.CHARACTER_ONLY_MASK | (1 << PhysicsLayers.OTHER_AVATARS_LAYER);

        private readonly DCLInput dclInput;

        private SingleInstanceEntity camera;

        private HandPointAtSystem(World world) : base(world)
        {
            dclInput = DCLInput.Instance;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            CancelPointAtIfEmotingQuery(World);
            UpdateHandPointAtQuery(World, in camera.GetCameraComponent(World), t);
            ApplyPointAtIKQuery(World, t);
        }

        private (bool isPressed, Vector2 pointerPos) HandleCursorLogic(ref HandPointAtComponent handPointAtComponent)
        {
            bool isPressed = dclInput.Player.PointAt.IsPressed();
            Vector2 pointerPos = dclInput.UI.Point.ReadValue<Vector2>();

            if (isPressed && !handPointAtComponent.WasPressed)
            {
                handPointAtComponent.PressOrigin = pointerPos;
                handPointAtComponent.IsDragging = false;
            }

            switch (isPressed)
            {
                case true when !handPointAtComponent.IsDragging:
                {
                    if ((pointerPos - handPointAtComponent.PressOrigin).sqrMagnitude > DRAG_THRESHOLD_SQR)
                        handPointAtComponent.IsDragging = true;

                    break;
                }
                case false when handPointAtComponent.WasPressed:
                {
                    if (handPointAtComponent.IsDragging)
                        handPointAtComponent.RefreshDuration(0f);

                    break;
                }
            }

            handPointAtComponent.WasPressed = isPressed;

            return (isPressed, pointerPos);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CancelPointAtIfEmoting(
            in CharacterEmoteComponent emoteComponent,
            ref HandPointAtComponent handPointAtComponent)
        {
            if (emoteComponent.IsPlayingEmote)
                handPointAtComponent.RefreshDuration(0f);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateHandPointAt(
            [Data] in CameraComponent cameraComponent,
            [Data] float dt,
            ref HandPointAtComponent handPointAtComponent,
            in CharacterRigidTransform rigidTransform,
            in StunComponent stunComponent,
            ref CharacterEmoteComponent emoteComponent,
            in CharacterPlatformComponent platformComponent,
            in ICharacterControllerSettings settings)
        {
            bool canPointAt = rigidTransform.IsGrounded
                              && !(rigidTransform.MoveVelocity.Velocity.sqrMagnitude > 0.5f)
                              && !stunComponent.IsStunned
                              && !platformComponent.PositionChanged;

            handPointAtComponent.TickDuration(dt);

            if (!canPointAt)
                handPointAtComponent.RefreshDuration(0f);

            var cursorInfo = HandleCursorLogic(ref handPointAtComponent);

            if (!canPointAt || !cursorInfo.isPressed)
                return;

            if (emoteComponent.IsPlayingEmote)
                emoteComponent.StopEmote = true;

            handPointAtComponent.RefreshDuration(settings.PointAtDuration);

            Ray ray = cameraComponent.Camera.ScreenPointToRay(cursorInfo.pointerPos);
            Vector3 hitPoint = Vector3.zero;

            if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, settings.PointAtMaxDistance, RAYCAST_LAYER_MASK))
                hitPoint = hit.point;
            else
            {
                Camera cam = cameraComponent.Camera;

                Plane farPlane = new Plane(
                    -cam.transform.forward,
                    cam.transform.position + (cam.transform.forward * cam.farClipPlane)
                );

                if (farPlane.Raycast(ray, out float enter))
                    hitPoint = ray.GetPoint(enter);
            }

            Debug.DrawLine(ray.origin, hitPoint, Color.red);

            handPointAtComponent.WorldHitPoint = hitPoint;

            handPointAtComponent.IsPointing = true;
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(RemotePlayerMovementComponent))]
        private void ApplyPointAtIK(
            [Data] float dt,
            ref HandPointAtComponent pointAt,
            ref AvatarBase avatarBase,
            ref CharacterRigidTransform rigidTransform,
            in ICharacterControllerSettings settings)
        {
            HandPointAtHelper.ApplyAnimationWeight(ref pointAt, ref avatarBase, in settings, dt);

            if (!pointAt.IsPointing)
            {
                pointAt.RotationAnimationWeight = 0f;
                HandPointAtHelper.SetPlayerRotationAnimation(ref avatarBase, pointAt.RotationAnimationWeight, false, false);
                return;
            }

            Vector3 shoulderPos = avatarBase.RightShoulderAnchorPoint.position;

            Vector3 directionToTarget = HandPointAtHelper.ClampElevation(
                (pointAt.WorldHitPoint - shoulderPos).normalized,
                settings.PointAtRotationVerticalUpThreshold,
                settings.PointAtRotationVerticalDownThreshold);

            var rotationInfo = HandPointAtHelper.CalculateAvatarRotation(avatarBase, settings, rigidTransform.LookDirection, directionToTarget);

            if (rotationInfo.needToRotate)
                rigidTransform.LookDirection = rotationInfo.newLookDirection;

            bool isActuallyRotating = rotationInfo.needToRotate && !Mathf.Approximately(rotationInfo.dot, 1f);
            Vector3 lookDirectionNormalized = rigidTransform.LookDirection.normalized;
            Vector3 cross = Vector3.Cross(avatarBase.transform.forward, lookDirectionNormalized);

            HandPointAtHelper.PlayerRotationAnimation(settings, ref avatarBase, ref pointAt, isActuallyRotating, dt, cross.y);

            HandPointAtHelper.ApplyHandIK(ref pointAt, ref avatarBase, in settings, dt, directionToTarget, shoulderPos, rotationInfo);
        }


    }
}
