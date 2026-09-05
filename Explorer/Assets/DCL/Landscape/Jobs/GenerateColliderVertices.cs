using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace DCL.Landscape.Jobs
{
    [BurstCompile]
    public struct GenerateColliderVertices : IJobParallelForBatch
    {
        public int OccupancyFloor;
        [ReadOnly] public NativeArray<byte> OccupancyMap;
        public int OccupancyMapSize;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> Parcels;
        public int ParcelSize;
        public float MaxHeight;
        [WriteOnly] public NativeArray<GroundColliderVertex> Vertices;

        public void Execute(int startIndex, int count)
        {
            int batchEnd = startIndex + count;
            int vertexIndex = startIndex;
            int sideVertexCount = ParcelSize + 1;
            int meshVertexCount = sideVertexCount * sideVertexCount;
            int meshIndex = startIndex / meshVertexCount;

            while (vertexIndex < batchEnd)
            {
                int2 parcel = Parcels[meshIndex];
                int2 parcelOriginXZ = parcel * ParcelSize;
                int meshStart = meshIndex * meshVertexCount;
                int meshEnd = min(meshStart + meshVertexCount, batchEnd);

                while (vertexIndex < meshEnd)
                {
                    int x = (vertexIndex - meshStart) % sideVertexCount;
                    int z = (vertexIndex - meshStart) / sideVertexCount;

                    float y = TerrainGenerator.GetHeight(x + parcelOriginXZ.x, z + parcelOriginXZ.y,
                        ParcelSize, OccupancyMap, OccupancyMapSize, OccupancyFloor, MaxHeight);

                    var vertex = new GroundColliderVertex() { Position = float3(x, y, z) };
#if UNITY_EDITOR // Only needed for drawing gizmos.
                    vertex.Normal = float3(0f, 1f, 0f);
#endif

                    Vertices[vertexIndex] = vertex;
                    vertexIndex++;
                }

                meshIndex++;
            }
        }
    }

    public struct GroundColliderVertex
    {
        public float3 Position;
#if UNITY_EDITOR // Normals are only needed to draw the collider gizmo.
        public float3 Normal;
#endif
    }
}
