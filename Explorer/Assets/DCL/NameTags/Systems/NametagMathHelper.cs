using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace DCL.Nametags
{
    [BurstCompile]
    public struct NametagMathHelper
    {
        private static readonly float3 FORWARD = new (0f, 0f, 1f);
        private static readonly float3 UP = new (0f, 1f, 0f);
        private static readonly float3 ONE = new (1f, 1f, 1f);
        private const float CHANGE_THRESHOLD = 0.0001f;
        private const float HALF_DEGREES_TO_RADIANS = 0.5f * math.PI / 180f;
        private const float FADE_START_RATIO = 0.5f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static float CalculateFovScaleFactor(float fieldOfView, float multiplier) =>
            math.tan(fieldOfView * HALF_DEGREES_TO_RADIANS) * multiplier;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static void CalculateCameraForward(in quaternion cameraRotation, out float3 result)
        {
            result = math.mul(cameraRotation, FORWARD);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static void CalculateCameraUp(in quaternion cameraRotation, out float3 result)
        {
            result = math.mul(cameraRotation, UP);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static bool IsOutOfRenderRange(in float3 cameraPos, in float3 characterPos, float maxDistanceSqr, float minDistanceSqr)
        {
            var distancesq = math.distancesq(cameraPos, characterPos);

            return distancesq > maxDistanceSqr || distancesq < minDistanceSqr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static void CalculateTagScale(float distance, float fovScaleFactor, out float3 result)
        {
            result = ONE * (fovScaleFactor * distance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static bool HasDistanceChanged(in float3 cameraPos, in float3 characterPos, float lastSqrDistance)
        {
            float newSqrDistance = math.distancesq(cameraPos, characterPos);
            return HasChanged(newSqrDistance, lastSqrDistance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static bool HasChanged(float value, float lastValue) =>
            math.abs(value - lastValue) >= CHANGE_THRESHOLD;

        /// <summary>
        ///     Fully opaque up to half of the max distance, then fades out linearly to zero at the max distance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static float CalculateOpacity(float distance, float maxDistance)
        {
            float fadeStartDistance = maxDistance * FADE_START_RATIO;
            return math.saturate(1f - ((distance - fadeStartDistance) / (maxDistance - fadeStartDistance)));
        }

        /// <summary>
        ///     Picks the first usable distance: the active virtual camera's, then the avatar's, then the default.
        ///     A non-positive, NaN or infinite value counts as unset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static float ResolveMaxDistance(float cameraMaxDistance, float avatarMaxDistance, float defaultMaxDistance)
        {
            if (IsValidMaxDistance(cameraMaxDistance))
                return cameraMaxDistance;

            return IsValidMaxDistance(avatarMaxDistance) ? avatarMaxDistance : defaultMaxDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static bool IsValidMaxDistance(float maxDistance) =>
            math.isfinite(maxDistance) && maxDistance > 0f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static void CalculateDistance(in float3 cameraPos, in float3 characterPos, out float distance, out float sqrDistance)
        {
            sqrDistance = math.distancesq(cameraPos, characterPos);
            distance = math.sqrt(sqrDistance);
        }
    }
}
