using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    public struct ClipVolume : IDisposable
    {
        public MinMaxAABB Bounds;
        public NativeArray<ClipPlane> Planes;

        private static readonly FixedList128Bytes<float3> CLIP_SPACE_CORNERS = new ()
        {
            new float3(-1f, -1f, 0f), new float3(-1f, -1f, 1f), new float3(-1f, 1f, 0f), new float3(-1f, 1f, 1f), new float3(1f, -1f, 0f),
            new float3(1f, -1f, 1f), new float3(1f, 1f, 0f), new float3(1f, 1f, 1f),
        };

        public ClipVolume(float4x4 worldToClip, Allocator allocator)
        {
            Planes = new NativeArray<ClipPlane>(6, allocator, NativeArrayOptions.UninitializedMemory);
            float4x4 rows = transpose(worldToClip);

            Planes[0] = new ClipPlane(rows.c3 + rows.c0);
            Planes[1] = new ClipPlane(rows.c3 - rows.c0);
            Planes[2] = new ClipPlane(rows.c3 + rows.c1);
            Planes[3] = new ClipPlane(rows.c3 - rows.c1);
            Planes[4] = new ClipPlane(rows.c3 + rows.c2);
            Planes[5] = new ClipPlane(rows.c3 - rows.c2);

            float4x4 clipToWorld = inverse(worldToClip);
            float3 corner0 = clipToWorld.MultiplyPoint(CLIP_SPACE_CORNERS[0]);
            Bounds = new MinMaxAABB(corner0, corner0);

            for (var i = 1; i < CLIP_SPACE_CORNERS.Length; i++)
                Bounds.Encapsulate(clipToWorld.MultiplyPoint(CLIP_SPACE_CORNERS[i]));
        }

        public void Dispose() =>
            Planes.Dispose();

        public bool Overlaps(MinMaxAABB bounds)
        {
            if (!bounds.Overlaps(Bounds))
                return false;

            for (var i = 0; i < Planes.Length; i++)
            {
                ClipPlane plane = Planes[i];
                float3 farCorner = bounds.GetCorner(plane.FarCornerIndex);

                if (plane.Plane.SignedDistanceToPoint(farCorner) < 0f)
                    return false;
            }

            return true;
        }
    }
}
