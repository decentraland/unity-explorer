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
        private const float DISTANCE_CHANGE_THRESHOLD = 0.0001f;
        private const float HALF_DEGREES_TO_RADIANS = 0.5f * math.PI / 180f;

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
        public static bool IsOutOfRenderRange(in float3 cameraPos, in float3 characterPos, float maxDistanceSqr) =>
            math.distancesq(cameraPos, characterPos) > maxDistanceSqr;

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
            return math.abs(newSqrDistance - lastSqrDistance) >= DISTANCE_CHANGE_THRESHOLD;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static void CalculateDistance(in float3 cameraPos, in float3 characterPos, out float distance, out float sqrDistance)
        {
            sqrDistance = math.distancesq(cameraPos, characterPos);
            distance = math.sqrt(sqrDistance);
        }
    }
}
