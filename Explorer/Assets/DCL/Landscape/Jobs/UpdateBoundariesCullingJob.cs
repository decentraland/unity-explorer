using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Jobs
{
    public struct VisibleBounds
    {
        public AABB Bounds;
        public bool IsVisible;
        public bool IsAtDistance;
        public bool IsDirty;
    }

    public struct TerrainAudioState
    {
        public bool IsSilent;
        public bool IsHeard;
        public bool ShouldBeSilent;
        public bool ShouldBeHeard;
    }

    [BurstCompile]
    public struct UpdateBoundariesCullingJob : IJobParallelFor
    {
        private NativeArray<VisibleBounds> terrainVisibilities;
        private NativeArray<TerrainAudioState> terrainAudioStates;
        private readonly float3 cameraPosition;
        private readonly float detailDistanceSqr;
        [ReadOnly] private NativeArray<float4> cameraPlanes;

        public UpdateBoundariesCullingJob(
            NativeArray<VisibleBounds> terrainVisibilities,
            NativeArray<TerrainAudioState> terrainAudioStates,
            NativeArray<float4> cameraPlanes,
            float3 cameraPosition,
            float detailDistance)
        {
            this.terrainVisibilities = terrainVisibilities;
            this.terrainAudioStates = terrainAudioStates;
            this.cameraPlanes = cameraPlanes;
            this.cameraPosition = cameraPosition;
            detailDistanceSqr = detailDistance * detailDistance;
        }

        public void Execute(int i)
        {
            VisibleBounds terrain = terrainVisibilities[i];
            TerrainAudioState audioState = terrainAudioStates[i];
            bool isVisible = TestPlanesAABB(terrain.Bounds);
            var isAtDistance = true;

            float sqrDistance = terrain.Bounds.DistanceSq(cameraPosition);

            if (isVisible) { isAtDistance = sqrDistance < detailDistanceSqr; }

            if (sqrDistance < 22500) //This value should come from settings 150^2
            {
                if (!audioState.IsHeard)
                {
                    audioState.ShouldBeHeard = true;
                    audioState.IsSilent = false;
                    audioState.ShouldBeSilent = false;
                }
            }
            else if (sqrDistance > 28900) //This value should come from settings 170^2
                                          //We do this so we are not removing AudioSources immediately after a player is out of range,
                                          //otherwise it might sound weird if player returns to a zone they just left
            {
                if (!audioState.IsSilent)
                {
                    audioState.IsHeard = false;
                    audioState.ShouldBeHeard = false;
                    audioState.ShouldBeSilent = true;
                }
            }

            terrain.IsDirty = terrain.IsVisible != isVisible || terrain.IsAtDistance != isAtDistance;
            terrain.IsVisible = isVisible;
            terrain.IsAtDistance = isAtDistance;

            terrainVisibilities[i] = terrain;
            terrainAudioStates[i] = audioState;
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
