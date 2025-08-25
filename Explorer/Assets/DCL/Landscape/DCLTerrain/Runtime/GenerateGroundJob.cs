using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    [BurstCompile]
    internal struct GenerateGroundJob : IJob
    {
        public TerrainDataData TerrainData;
        public float3 CameraPosition;
        [ReadOnly] public ClipVolume CameraFrustum;
        public NativeArray<int> InstanceCounts;
        public NativeList<Matrix4x4> Transforms;

        // x and y are relative parcel coordinates, z is the mesh to use (0 is middle piece, 1 is edge
        // piece, and 2 is corner piece), and w is the rotation around the Y axis. Ground meshes are to
        // be placed in a 2x2 square (items 0 to 3) and then in an ever expanding concentric rings
        // around that (items 4 to 15), doubling the parcel coordinate values after every iteration.
        private static readonly FixedList512Bytes<int4> MAGIC_PATTERN = new ()
        {
            int4(0, 0, 0, 0), int4(-1, 0, 0, 0), int4(-1, -1, 0, 0), int4(0, -1, 0, 0),
            int4(1, 1, 2, 0), int4(0, 1, 1, 0), int4(-1, 1, 1, 0), int4(-2, 1, 2, -90),
            int4(-2, 0, 1, -90), int4(-2, -1, 1, -90), int4(-2, -2, 2, 180), int4(-1, -2, 1, 180),
            int4(0, -2, 1, 180), int4(1, -2, 2, 90), int4(1, -1, 1, 90), int4(1, 0, 1, 90),
        };

        public void Execute()
        {
            int2 origin = (PositionToParcel(CameraPosition) + 1) & ~1;
            int scale = (int)(CameraPosition.y / TerrainData.ParcelSize) + 1;
            var instances = new NativeList<GroundInstance>(Transforms.Capacity, Allocator.Temp);

            for (var i = 0; i < 4; i++)
                TryGenerateGround(origin, MAGIC_PATTERN[i], scale, instances);

            while (true)
            {
                var stop = true;

                for (var i = 4; i < 16; i++)
                    if (TryGenerateGround(origin, MAGIC_PATTERN[i], scale, instances))
                        stop = false;

                if (stop || scale >= int.MaxValue / 2)
                    break;

                scale *= 2;
            }

            instances.Sort();

            if (Transforms.Capacity < instances.Length)
                Transforms.Capacity = instances.Length;

            var instanceCount = 0;
            var meshIndex = 0;

            for (var instanceIndex = 0; instanceIndex < instances.Length; instanceIndex++)
            {
                GroundInstance instance = instances[instanceIndex];

                if (meshIndex < instance.meshIndex)
                {
                    InstanceCounts[meshIndex] = instanceCount;
                    meshIndex = instance.meshIndex;
                    instanceCount = 0;
                }

                instanceCount++;

                Transforms.AddNoResize(Matrix4x4.TRS(
                    new Vector3(instance.positionXZ.x, 0f, instance.positionXZ.y),
                    Quaternion.Euler(0f, instance.rotationY, 0f),
                    new Vector3(instance.scale, instance.scale, instance.scale)));
            }

            InstanceCounts[meshIndex] = instanceCount;
            instances.Dispose();
        }

        private int2 PositionToParcel(float3 value) =>
            (int2)floor(value.xz * (1f / TerrainData.ParcelSize));

        private bool TryGenerateGround(int2 origin, int4 magic, int scale,
            NativeList<GroundInstance> instances)
        {
            int2 min = origin + (magic.xy * scale);
            int2 max = min + scale;
            int parcelSize = TerrainData.ParcelSize;

            var bounds = new MinMaxAABB(float3(min.x * parcelSize, 0f, min.y * parcelSize),
                float3(max.x * parcelSize, TerrainData.maxHeight, max.y * parcelSize));

            if (!CameraFrustum.Overlaps(bounds))
                return false;

            if (!TerrainData.BoundsOverlaps(int4(min, scale, scale)))

                // Skip this instance, but keep generating. The case to consider is when the camera
                // is far outside the bounds of the terrain.
                return true;

            instances.Add(new GroundInstance
            {
                meshIndex = magic.z,
                positionXZ = bounds.Center.xz,
                rotationY = magic.w,
                scale = scale,
            });

            return true;
        }

        private struct GroundInstance : IComparable<GroundInstance>
        {
            public int meshIndex;
            public float2 positionXZ;
            public float rotationY;
            public float scale;

            public int CompareTo(GroundInstance other) =>
                meshIndex - other.meshIndex;
        }
    }
}
