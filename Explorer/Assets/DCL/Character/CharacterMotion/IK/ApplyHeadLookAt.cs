using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.CharacterMotion.IK
{
    [BurstCompile]
    public static class ApplyHeadLookAt
    {
        private const float TWO_PI = math.PI * 2;
        private const float THREE_PI = math.PI * 3;

        /// <summary>
        /// This method updates the head IK targets (horizontal and vertical) based on a target look-at direction
        /// </summary>
        /// <param name="useFrontalReset"> If the target horizonal angle is outside of the limits, reset the head location to look frontal </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(Vector3 targetDirection, AvatarBase avatarBase, float dt, ICharacterControllerSettings settings, bool useFrontalReset = true)
        {
            Execute(avatarBase.HeadPositionConstraint.forward,
                targetDirection,
                settings.HeadIKRotationSpeed * dt,
                avatarBase.HeadLookAtTargetHorizontal.localRotation,
                avatarBase.HeadLookAtTargetVertical.localRotation,
                new float2(settings.HeadIKHorizontalAngleLimit, settings.HeadIKVerticalAngleLimit),
                useFrontalReset,
                settings.HeadIKHorizontalAngleReset,
                out var horizontalRotationResult,
                out var verticalRotationResult);

            avatarBase.HeadLookAtTargetHorizontal.localRotation = horizontalRotationResult;
            avatarBase.HeadLookAtTargetVertical.localRotation = verticalRotationResult;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Execute(in float3 referenceDirection,
            in float3 targetDirection,
            float maxDeltaDegrees,
            in quaternion lookAtTargetHorizontal,
            in quaternion lookAtTargetVertical,
            in float2 angleLimitsDegrees,
            bool useFrontalReset,
            float angleResetDegrees,
            out quaternion horizontalRotationResult,
            out quaternion verticalRotationResult)
        {
            float3 up = math.up();
            float3 referenceAngle = math.Euler(quaternion.LookRotation(referenceDirection, up));
            float3 targetAngle = math.Euler(quaternion.LookRotation(targetDirection, up));

            float horizontalAngle = DeltaAngle(referenceAngle.y, targetAngle.y);

            quaternion horizontalTargetRotation;
            quaternion verticalTargetRotation;

            if (useFrontalReset && math.abs(horizontalAngle) > math.radians(angleResetDegrees))
            {
                horizontalTargetRotation = quaternion.identity;
                verticalTargetRotation = quaternion.identity;
                maxDeltaDegrees *= 0.333333f;
            }
            else
            {
                var angleLimitsRads = math.radians(angleLimitsDegrees);

                horizontalAngle = math.clamp(horizontalAngle, -angleLimitsRads.x, angleLimitsRads.x);
                horizontalTargetRotation = quaternion.AxisAngle(up, horizontalAngle);

                float verticalAngle = DeltaAngle(referenceAngle.x, targetAngle.x);
                verticalAngle = math.clamp(verticalAngle, -angleLimitsRads.y, angleLimitsRads.y);
                verticalTargetRotation = math.mul(horizontalTargetRotation, quaternion.AxisAngle(math.right(), verticalAngle));
            }

            float maxDelta = math.radians(maxDeltaDegrees);
            horizontalRotationResult = RotateTowards(lookAtTargetHorizontal, horizontalTargetRotation, maxDelta);
            verticalRotationResult = RotateTowards(lookAtTargetVertical, verticalTargetRotation, maxDelta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DeltaAngle(float referenceAngle, float targetAngle) =>
            math.fmod(targetAngle - referenceAngle + THREE_PI, TWO_PI) - math.PI;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion RotateTowards(quaternion current, quaternion target, float maxDelta)
        {
            float dot = math.dot(current, target);
            if (dot < 0)
            {
                target.value = -target.value;
                dot = -dot;
            }
            float angle = math.acos(math.clamp(dot, -1, 1)) * 2;
            return angle < 1e-6f ? target : math.slerp(current, target, math.min(1, maxDelta / angle));
        }
    }
}
