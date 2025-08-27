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
        public float MaxHeight;
        [ReadOnly] public NativeArray<byte> OccupancyMap;
        public int OccupancyMapSize;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> Parcels;
        public int ParcelSize;
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
                    float y = GetHeight(x + parcelOriginXZ.x, z + parcelOriginXZ.y);

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

        public float GetHeight(float x, float z)
        {
            float occupancy;

            if (OccupancyMapSize > 0)
            {
                // Take the bounds of the terrain, put a single pixel border around it, increase the
                // size to the next power of two, map xz=0,0 to uv=0.5,0.5 and parcelSize to pixel size,
                // and that's the occupancy map.
                float2 uv = ((float2(x, z) / ParcelSize) + (OccupancyMapSize * 0.5f)) / OccupancyMapSize;
                occupancy = SampleBilinearClamp(OccupancyMap, int2(OccupancyMapSize, OccupancyMapSize), uv);
            }
            else
            {
                occupancy = 0f;
            }

            // float height = SAMPLE_TEXTURE2D_LOD(HeightMap, HeightMap.samplerstate, uv, 0.0).r;
            float minValue = 175.0f / 255.0f; // 0.68

            // In the "worst case", if occupancy is 0.25, it can mean that the current vertex is on a
            // corner between one occupied parcel and three free ones, and height must be zero.
            if (occupancy <= minValue)
            {
                return 0f;
            }
            else
            {
                float normalizedHeight = (occupancy - minValue) / (1 - minValue);
                return normalizedHeight * MaxHeight;// + noiseH * transitionFactor;
            }
        }

        private static float SampleBilinearClamp(NativeArray<byte> texture, int2 textureSize, float2 uv)
        {
            uv = (uv * textureSize) - 0.5f;
            int2 min = (int2)floor(uv);

            // A quick prayer for Burst to SIMD this. 🙏
            int4 index = (clamp(min.y + int4(1, 1, 0, 0), 0, textureSize.y - 1) * textureSize.x) +
                         clamp(min.x + int4(0, 1, 1, 0), 0, textureSize.x - 1);

            float2 t = frac(uv);
            float top = lerp(texture[index.w], texture[index.z], t.x);
            float bottom = lerp(texture[index.x], texture[index.y], t.x);
            return lerp(top, bottom, t.y) * (1f / 255f);
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
