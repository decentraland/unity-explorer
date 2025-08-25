using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace DCL.Nametags
{
    [BurstCompile]
    public struct NametagMathHelper
    {
        private static readonly float3 ONE = new (1f, 1f, 1f);
        private const float DISTANCE_CHANGE_THRESHOLD = 0.0001f; // TODO: Less

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static bool IsOutOfRenderRange(in float3 cameraPos, in float3 characterPos, float maxDistanceSqr) =>
            math.distancesq(cameraPos, characterPos) > maxDistanceSqr;

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
