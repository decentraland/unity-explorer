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
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    public partial class HandPointAtSystem : BaseUnityLoopSystem
    {
        private struct CursorInfo
        {
            public bool isPressed;
            public bool isFreshPress;
            public Vector2 pointerPos;
        }

        private struct HitInfo
        {
            public bool ForceInterrupt;
            public Vector3 HitPoint;

            public static HitInfo Empty() => new (false, Vector3.zero);

            public HitInfo(bool forceInterrupt, Vector3 hitPoint)
            {
                ForceInterrupt = forceInterrupt;
                HitPoint = hitPoint;
            }
        }

        private const float DRAG_THRESHOLD_SQR = 5f * 5f;
        private const float AVATAR_MAX_DISTANCE_SQR = 2f * 2f;
        private const float MIN_HIT_POINT_DISTANCE_SQR = 1f * 1f;
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

        private CursorInfo HandleCursorLogic(ref HandPointAtComponent handPointAtComponent)
        {
            bool isPressed = dclInput.Player.PointAt.IsPressed();
            Vector2 pointerPos = dclInput.UI.Point.ReadValue<Vector2>();
            bool isFreshPress = isPressed && !handPointAtComponent.WasPressed;

            if (isFreshPress)
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

            if (handPointAtComponent.ForceStop)
            {
                isPressed = false;

                if (!dclInput.Player.PointAt.IsPressed())
                    handPointAtComponent.ForceStop = false;
            }

            return new CursorInfo {
                isPressed = isPressed,
                isFreshPress = isFreshPress,
                pointerPos = pointerPos
            };
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CancelPointAtIfEmoting(
            in CharacterEmoteComponent emoteComponent,
            ref HandPointAtComponent handPointAtComponent)
        {
            if (emoteComponent.IsPlayingEmote && handPointAtComponent is { StoppedEmote: false, IsPointing: true })
                handPointAtComponent.ForceStopAction();
        }

        private HitInfo PerformRaycast(
            CursorInfo cursorInfo,
            in CameraComponent cameraComponent,
            in ICharacterControllerSettings settings,
            in AvatarBase avatarBase,
            in CharacterController characterController)
        {
            HitInfo result = HitInfo.Empty();
            Camera cam = cameraComponent.Camera;

            Ray ray = cameraComponent.Camera.ScreenPointToRay(cursorInfo.pointerPos);

            // Did we hit anything with a collider?
            if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, settings.PointAtMaxDistance, RAYCAST_LAYER_MASK))
            {
                if (hit.collider.gameObject.layer == PhysicsLayers.OTHER_AVATARS_LAYER)
                {
                    // If we hit a remote avatar, the user must be far from us (2m away) otherwise we interrupt the pointing action to avoid "weird" interactions
                    if ((hit.point - avatarBase.transform.position).sqrMagnitude <= AVATAR_MAX_DISTANCE_SQR)
                    {
                        result.ForceInterrupt = true;
                        return result;
                    }

                    // If we hit a player, project the hit-point to the far clipping plane so that it visually points the player but the
                    // actual point cannot be rendered
                    Vector3 shoulderPos = avatarBase.RightShoulderAnchorPoint.position;
                    Vector3 dirFromShoulder = (hit.point - shoulderPos).normalized;

                    result.HitPoint = shoulderPos + (dirFromShoulder * cam.farClipPlane);
                }
                else
                    // Random scenery -> hit point is valid
                    result.HitPoint = hit.point;

                Vector3 avatarMidHeight = avatarBase.transform.position + (Vector3.up * (characterController.height / 2f));
                result.ForceInterrupt = (result.HitPoint - avatarMidHeight).sqrMagnitude <= MIN_HIT_POINT_DISTANCE_SQR;
            }
            // Fallback: If we didn't hit anything with a collider (either a mesh without one or the actual sky), we just hit the far clipping plane
            else
            {
                Plane farPlane = new Plane(
                    -cam.transform.forward,
                    cam.transform.position + (cam.transform.forward * cam.farClipPlane)
                );

                if (farPlane.Raycast(ray, out float enter))
                    result.HitPoint = ray.GetPoint(enter);
            }

#if UNITY_EDITOR
            Debug.DrawLine(ray.origin, result.HitPoint, Color.red);
#endif
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryStopEmote(ref HandPointAtComponent handPointAtComponent,
            ref CharacterEmoteComponent emoteComponent,
            CursorInfo cursorInfo)
        {
            if (cursorInfo.isFreshPress && emoteComponent.IsPlayingEmote)
            {
                emoteComponent.StopEmote = true;
                handPointAtComponent.ForceStop = false;
                handPointAtComponent.StoppedEmote = true;
            }

            if (handPointAtComponent.StoppedEmote && !emoteComponent.IsPlayingEmote)
                handPointAtComponent.StoppedEmote = false;
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
            in ICharacterControllerSettings settings,
            in AvatarBase avatarBase,
            in CharacterController characterController)
        {
            bool canPointAt = rigidTransform.IsGrounded
                              && !(rigidTransform.MoveVelocity.Velocity.sqrMagnitude > 0.5f)
                              && !stunComponent.IsStunned
                              && !platformComponent.PositionChanged;

            handPointAtComponent.TickDuration(dt);

            if (!canPointAt)
                handPointAtComponent.RefreshDuration(0f);

            var cursorInfo = HandleCursorLogic(ref handPointAtComponent);

            TryStopEmote(ref handPointAtComponent, ref emoteComponent, cursorInfo);

            if (!canPointAt || !cursorInfo.isPressed)
                return;

            HitInfo hitInfo = PerformRaycast(cursorInfo, cameraComponent, settings, avatarBase, characterController);

            if (hitInfo.ForceInterrupt)
            {
                handPointAtComponent.ForceStopAction();
                return;
            }

            handPointAtComponent.WorldHitPoint = hitInfo.HitPoint;
            handPointAtComponent.IsPointing = true;
            handPointAtComponent.RefreshDuration(settings.PointAtDuration);
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
