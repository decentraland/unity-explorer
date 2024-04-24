using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.Jobs
{
    public struct VisibleBounds
    {
        public AABB Bounds;
        public bool IsVisible;
        public bool IsAtDistance;
        public bool IsDirty;
    }

    [BurstCompile]
    public struct UpdateBoundariesCullingJob : IJobParallelFor
    {
        private NativeArray<VisibleBounds> terrainVisibilities;
        private readonly float3 cameraPosition;
        private readonly float detailDistanceSqr;
        [ReadOnly] private NativeArray<float4> cameraPlanes;

        public UpdateBoundariesCullingJob(
            NativeArray<VisibleBounds> terrainVisibilities,
            NativeArray<float4> cameraPlanes,
            float3 cameraPosition,
            float detailDistance)
        {
            this.terrainVisibilities = terrainVisibilities;
            this.cameraPlanes = cameraPlanes;
            this.cameraPosition = cameraPosition;
            detailDistanceSqr = detailDistance * detailDistance;
        }

        public void Execute(int i)
        {
            VisibleBounds terrain = terrainVisibilities[i];
            bool isVisible = TestPlanesAABB(terrain.Bounds);
            var isAtDistance = true;

            if (isVisible)
            {
                float sqrDist = terrain.Bounds.DistanceSq(cameraPosition);
                isAtDistance = sqrDist < detailDistanceSqr;
            }

            terrain.IsDirty = terrain.IsVisible != isVisible || terrain.IsAtDistance != isAtDistance;
            terrain.IsVisible = isVisible;
            terrain.IsAtDistance = isAtDistance;

            terrainVisibilities[i] = terrain;
        }

        // got this one from https://forum.unity.com/threads/managed-version-of-geometryutility-testplanesaabb.473575/
        // thanks kind internet fellow :)
        private bool TestPlanesAABB(AABB bounds)
        {
            for (var i = 0; i < cameraPlanes.Length; i++)
            {
                float4 plane = cameraPlanes[i];
                float3 planeNormal = plane.xyz;
                float planeDistance = plane.w;
                float3 normalSign = math.sign(planeNormal);
                float3 testPoint = bounds.Center + (bounds.Extents * normalSign);

                float dot = math.dot(testPoint, planeNormal);

                if (dot + planeDistance < 0)
                    return false;
            }

            return true;
        }
    }
}
