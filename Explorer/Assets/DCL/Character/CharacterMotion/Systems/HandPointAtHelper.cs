using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Animations;

namespace DCL.Character.CharacterMotion.Systems
{
    public static class HandPointAtHelper
    {
        public struct RotationInfo
        {
            public float dot;
            public bool needToRotate;
            public Vector3 newLookDirection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyAnimationWeight(
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PlayerRotationAnimation(
            in ICharacterControllerSettings settings,
            ref AvatarBase avatarBase,
            ref HandPointAtComponent pointAt,
            bool needToRotate,
            float dt,
            float crossY)
        {
            pointAt.RotationAnimationWeight = Mathf.MoveTowards(
                pointAt.RotationAnimationWeight, needToRotate ? 1f : 0f, settings.HandsIKWeightSpeed * dt);

            SetPlayerRotationAnimation(ref avatarBase, pointAt.RotationAnimationWeight, needToRotate && crossY <= 0, needToRotate && crossY > 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPlayerRotationAnimation(ref AvatarBase avatarBase, float weight, bool left, bool right)
        {
            avatarBase.SetRotationLayerWeight(weight);
            avatarBase.SetAnimatorBool(AnimationHashes.ROTATING_LEFT, left);
            avatarBase.SetAnimatorBool(AnimationHashes.ROTATING_RIGHT, right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClampElevation(Vector3 direction, float maxUp, float maxDown)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyHandIK(
            ref HandPointAtComponent pointAt,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            float dt,
            Vector3 directionToTarget,
            Vector3 shoulderPos,
            RotationInfo rotationInfo,
            bool overrideSpeed = false)
        {
            Vector3 ikTargetPos = shoulderPos + (directionToTarget * settings.PointAtArmReach);

            pointAt.RotationCompleted = rotationInfo.dot > 0.9f || pointAt.IsDragging || !rotationInfo.needToRotate;
            avatarBase.RightHandIK.weight = Mathf.MoveTowards(
                avatarBase.RightHandIK.weight, pointAt.RotationCompleted ? 1 : 0, settings.HandsIKWeightSpeed * dt);

            Transform target = avatarBase.RightHandSubTarget;

            float ikSpeed = pointAt.IsDragging || overrideSpeed ? settings.IKPositionSpeed : settings.IKPositionSpeed / 2;
            target.position = Vector3.MoveTowards(
                target.position, ikTargetPos, ikSpeed * dt);

            Vector3 pointDirection = (ikTargetPos - avatarBase.RightShoulderAnchorPoint.position).normalized;

            Vector3 backOfHand = Vector3.up - Vector3.Dot(Vector3.up, pointDirection) * pointDirection;

            if (backOfHand.sqrMagnitude < 0.001f)
                backOfHand = Vector3.forward - Vector3.Dot(Vector3.forward, pointDirection) * pointDirection;

            backOfHand.Normalize();

            target.rotation = Quaternion.LookRotation(-backOfHand, pointDirection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RotationInfo CalculateAvatarRotation(
            in AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            Vector3 lookDirection,
            Vector3 directionToTarget
        )
        {
            Vector3 dirHorizontal = new Vector3(directionToTarget.x, 0f, directionToTarget.z);
            float horizontalMag = dirHorizontal.magnitude;

            float crossY, dotH;

            Vector3 lookH = new Vector3(lookDirection.x, 0f, lookDirection.z);
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

            Vector3 newLookDirection = Vector3.zero;
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

                newLookDirection = ((c * dirH) + (s * perpH)) / m;
            }

            return new RotationInfo
            {
                dot = Vector3.Dot(avatarBase.transform.forward, needToRotate ? newLookDirection : lookDirection),
                needToRotate = needToRotate,
                newLookDirection = newLookDirection
            };
        }
    }
}
