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
using Utility.Animations;

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
        private readonly ICharacterControllerSettings localSettings;

        private SingleInstanceEntity camera;

        private HandPointAtSystem(World world, ICharacterControllerSettings localSettings) : base(world)
        {
            dclInput = DCLInput.Instance;
            this.localSettings = localSettings;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            CancelPointAtIfEmotingQuery(World);
            UpdateHandPointAtQuery(World, in camera.GetCameraComponent(World), t);
            ApplyLocalPointAtIKQuery(World, t);
            ApplyRemotePointAtIKQuery(World, t);
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

        private void PlayerRotationAnimation(
            ref AvatarBase avatarBase,
            ref HandPointAtComponent pointAt,
            bool needToRotate,
            float dt,
            float crossY)
        {
            pointAt.RotationAnimationWeight = Mathf.MoveTowards(
                pointAt.RotationAnimationWeight, needToRotate ? 1f : 0f, localSettings.HandsIKWeightSpeed * dt);

            SetPlayerRotationAnimation(ref avatarBase, pointAt.RotationAnimationWeight, needToRotate && crossY <= 0, needToRotate && crossY > 0);
        }

        private void SetPlayerRotationAnimation(ref AvatarBase avatarBase, float weight, bool left, bool right)
        {
            avatarBase.SetRotationLayerWeight(weight);
            avatarBase.SetAnimatorBool(AnimationHashes.ROTATING_LEFT, left);
            avatarBase.SetAnimatorBool(AnimationHashes.ROTATING_RIGHT, right);
        }

        private (float dot, bool needToRotate) HandleAvatarRotation(
            in AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            Vector3 directionToTarget
        )
        {
            Vector3 dirHorizontal = new Vector3(directionToTarget.x, 0f, directionToTarget.z);
            float horizontalMag = dirHorizontal.magnitude;

            float crossY, dotH;

            Vector3 lookH = new Vector3(rigidTransform.LookDirection.x, 0f, rigidTransform.LookDirection.z);
            float lookHMag = lookH.magnitude;

            if (horizontalMag > 1e-6f && lookHMag > 1e-6f)
            {
                Vector3 dirHNorm = dirHorizontal / horizontalMag;
                Vector3 lookHNorm = lookH / lookHMag;
                crossY = Vector3.Cross(lookHNorm, dirHNorm).y;
                dotH = Vector3.Dot(lookHNorm, dirHNorm);
            }
            else
            {
                crossY = 0f;
                dotH = 1f;
            }

            // crossY > 0 rotate right, else rotate left
            bool needToRotate = crossY > settings.PointAtRotationHorizontalRightThreshold
                                || crossY < -settings.PointAtRotationHorizontalLeftThreshold
                                || dotH < 0;

            if (needToRotate)
            {
                float targetCrossY = 0f;

                if (crossY > settings.PointAtRotationHorizontalRightThreshold)
                    targetCrossY = settings.PointAtRotationHorizontalRightThreshold;
                else if (crossY < -settings.PointAtRotationHorizontalLeftThreshold)
                    targetCrossY = -settings.PointAtRotationHorizontalLeftThreshold;
                else if (dotH < 0)
                    targetCrossY = crossY >= 0
                        ? settings.PointAtRotationHorizontalRightThreshold
                        : -settings.PointAtRotationHorizontalLeftThreshold;

                Vector3 dirH = new Vector3(directionToTarget.x, 0f, directionToTarget.z);
                Vector3 perpH = Vector3.Cross(directionToTarget, Vector3.up);
                float m = perpH.magnitude;

                float s = Mathf.Clamp(targetCrossY, -1f, 1f);
                float c = Mathf.Sqrt(1f - (s * s));

                rigidTransform.LookDirection = ((c * dirH) + (s * perpH)) / m;
            }

            return (Vector3.Dot(avatarBase.transform.forward, rigidTransform.LookDirection), needToRotate);
        }

        private void ApplyAnimationWeight(
            ref HandPointAtComponent pointAt,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            float dt)
        {
            float targetAnimWeight = pointAt is { IsPointing: true, RotationCompleted: true } ? 1f : 0f;

            pointAt.AnimationWeight = Mathf.MoveTowards(
                pointAt.AnimationWeight, targetAnimWeight, settings.HandsIKWeightSpeed * dt);

            avatarBase.SetPointAtLayerWeight(pointAt.AnimationWeight);
        }

        private static Vector3 ClampElevation(Vector3 direction, float maxUp, float maxDown)
        {
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            float horizontalMag = horizontal.magnitude;

            if (horizontalMag < 1e-6f)
                return direction;

            float elevation = Mathf.Atan2(direction.y, horizontalMag);
            float clamped = Mathf.Clamp(elevation, -maxDown, maxUp);

            if (Mathf.Approximately(elevation, clamped))
                return direction;

            Vector3 horizontalNorm = horizontal / horizontalMag;
            return (horizontalNorm * Mathf.Cos(clamped)) + (Vector3.up * Mathf.Sin(clamped));
        }

        private void ApplyHandIK(
            ref HandPointAtComponent pointAt,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            float dt,
            Vector3 directionToTarget,
            Vector3 shoulderPos,
            (float dot, bool needToRotate) rotationInfo)
        {
            Vector3 ikTargetPos = shoulderPos + (directionToTarget * settings.PointAtArmReach);

            pointAt.RotationCompleted = rotationInfo.dot > 0.9f || pointAt.IsDragging || !rotationInfo.needToRotate;
            avatarBase.RightHandIK.weight = Mathf.MoveTowards(
                avatarBase.RightHandIK.weight, pointAt.RotationCompleted ? 1 : 0, settings.HandsIKWeightSpeed * dt);

            Transform target = avatarBase.RightHandSubTarget;

            float ikSpeed = pointAt.IsDragging ? settings.IKPositionSpeed : settings.IKPositionSpeed / 2;
            target.position = Vector3.MoveTowards(
                target.position, ikTargetPos, ikSpeed * dt);

            Vector3 pointDirection = (ikTargetPos - avatarBase.RightShoulderAnchorPoint.position).normalized;

            Vector3 backOfHand = Vector3.up - Vector3.Dot(Vector3.up, pointDirection) * pointDirection;

            if (backOfHand.sqrMagnitude < 0.001f)
                backOfHand = Vector3.forward - Vector3.Dot(Vector3.forward, pointDirection) * pointDirection;

            backOfHand.Normalize();

            target.rotation = Quaternion.LookRotation(-backOfHand, pointDirection);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(RemotePlayerMovementComponent))]
        private void ApplyLocalPointAtIK(
            [Data] float dt,
            ref HandPointAtComponent pointAt,
            ref AvatarBase avatarBase,
            ref CharacterRigidTransform rigidTransform,
            in ICharacterControllerSettings settings)
        {
            ApplyAnimationWeight(ref pointAt, ref avatarBase, in settings, dt);

            if (!pointAt.IsPointing)
            {
                pointAt.RotationAnimationWeight = 0f;
                SetPlayerRotationAnimation(ref avatarBase, pointAt.RotationAnimationWeight, false, false);
                return;
            }

            Vector3 shoulderPos = avatarBase.RightShoulderAnchorPoint.position;

            Vector3 directionToTarget = ClampElevation(
                (pointAt.WorldHitPoint - shoulderPos).normalized,
                settings.PointAtRotationVerticalUpThreshold,
                settings.PointAtRotationVerticalDownThreshold);

            var rotationInfo = HandleAvatarRotation(avatarBase, settings, ref rigidTransform, directionToTarget);

            bool isActuallyRotating = rotationInfo.needToRotate && !Mathf.Approximately(rotationInfo.dot, 1f);
            Vector3 lookDirectionNormalized = rigidTransform.LookDirection.normalized;
            Vector3 cross = Vector3.Cross(avatarBase.transform.forward, lookDirectionNormalized);

            PlayerRotationAnimation(ref avatarBase, ref pointAt, isActuallyRotating, dt, cross.y);

            ApplyHandIK(ref pointAt, ref avatarBase, in settings, dt, directionToTarget, shoulderPos, rotationInfo);
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
            ApplyAnimationWeight(ref pointAt, ref avatarBase, in localSettings, dt);
            avatarBase.RightHandIK.weight = pointAt.AnimationWeight;
            avatarBase.HandsIKRig.weight = pointAt.AnimationWeight;

            if (!pointAt.IsPointing)
            {
                pointAt.RotationAnimationWeight = 0f;
                pointAt.PreviousLookDirection = Vector3.zero;
                SetPlayerRotationAnimation(ref avatarBase, pointAt.RotationAnimationWeight, false, false);
                return;
            }

            Vector3 shoulderPos = avatarBase.RightShoulderAnchorPoint.position;

            Vector3 directionToTarget = ClampElevation(
                (pointAt.WorldHitPoint - shoulderPos).normalized,
                localSettings.PointAtRotationVerticalUpThreshold,
                localSettings.PointAtRotationVerticalDownThreshold);

            if (pointAt.PreviousLookDirection != Vector3.zero)
            {
                Vector3 cross = Vector3.Cross(avatarBase.transform.forward, pointAt.PreviousLookDirection);
                float dot  = Vector3.Dot(avatarBase.transform.forward, pointAt.PreviousLookDirection);
                PlayerRotationAnimation(ref avatarBase, ref pointAt, !Mathf.Approximately(dot, 1f), dt, cross.y);
            }

            ApplyHandIK(ref pointAt, ref avatarBase, in localSettings, dt, directionToTarget, shoulderPos, (1f, false));

            pointAt.PreviousLookDirection = avatarBase.transform.forward;
        }
    }
}
