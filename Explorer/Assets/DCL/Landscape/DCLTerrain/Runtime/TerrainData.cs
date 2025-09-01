using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    public abstract class TerrainData : ScriptableObject
    {
        [field: SerializeField] public int ParcelSize { get; private set; }
        [field: SerializeField] public RectInt Bounds { get; set; }
        [field: SerializeField] public float MaxHeight { get; private set; }
        [field: SerializeField] public Texture2D OccupancyMap { get; set; }
        [field: SerializeField] public float DetailDistance { get; set; }
        [field: SerializeField] public GrassPrototype[] GrassPrototypes { get; private set; }
        [field: SerializeField] public FlowerPrototype[] FlowerPrototypes { get; private set; }
        public int OccupancyFloor { private get; set; }

        protected FunctionPointer<GetHeightDelegate> getHeight;

        private const int TERRAIN_SIZE_LIMIT = 512; // 512x512 parcels

        private void OnValidate()
        {
            RectInt bounds = Bounds;
            bounds.width = clamp(bounds.width, 0, TERRAIN_SIZE_LIMIT);
            bounds.height = clamp(bounds.height, 0, TERRAIN_SIZE_LIMIT);
            Bounds = bounds;
            ParcelSize = max(ParcelSize, 1);
        }

        protected abstract void CompileNoiseFunctions();

        public TerrainDataData GetData()
        {
            if (!getHeight.IsCreated)
                CompileNoiseFunctions();

            return new TerrainDataData(ParcelSize, MaxHeight, OccupancyMap, getHeight, OccupancyFloor);
        }
    }

    public readonly struct TerrainDataData
    {
        public readonly int ParcelSize;
        public readonly float MaxHeight;
        [ReadOnly] private readonly NativeArray<byte> occupancyMap;
        private readonly int occupancyMapSize;
        private readonly FunctionPointer<GetHeightDelegate> getHeight;
        private readonly int occupancyFloor;
        private static NativeArray<byte> emptyOccupancyMap;

        public TerrainDataData(int parcelSize, float maxHeight, Texture2D occupancyMap,
            FunctionPointer<GetHeightDelegate> getHeight, int occupancyFloor)
        {
            ParcelSize = parcelSize;
            this.MaxHeight = maxHeight;
            this.getHeight = getHeight;
            this.occupancyFloor = occupancyFloor;

            if (IsPowerOfTwo(occupancyMap, out occupancyMapSize))
                this.occupancyMap = occupancyMap.GetRawTextureData<byte>();
            else
            {
                if (!emptyOccupancyMap.IsCreated)
                    emptyOccupancyMap = new NativeArray<byte>(0, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);

                this.occupancyMap = emptyOccupancyMap;
            }
        }

        public float GetHeight(float x, float z)
        {
            float occupancy;

            if (occupancyMapSize > 0)
            {
                // Take the bounds of the terrain, put a single pixel border around it, increase the
                // size to the next power of two, map xz=0,0 to uv=0.5,0.5 and parcelSize to pixel size,
                // and that's the occupancy map.
                float2 uv = ((float2(x, z) / ParcelSize) + (occupancyMapSize * 0.5f)) / occupancyMapSize;
                occupancy = SampleBilinearClamp(occupancyMap, int2(occupancyMapSize, occupancyMapSize), uv);
            }
            else
                occupancy = 0f;

            // float height = SAMPLE_TEXTURE2D_LOD(HeightMap, HeightMap.samplerstate, uv, 0.0).r;
            float minValue = occupancyFloor / 255.0f; // 0.68

            if (occupancy <= minValue) return 0f;

            const float SATURATION_FACTOR = 20;
            float normalizedHeight = (occupancy - minValue) / (1 - minValue);
            return (normalizedHeight * MaxHeight) + (getHeight.Invoke(x, z) * saturate(normalizedHeight * SATURATION_FACTOR));
        }

        private static bool IsPowerOfTwo(Texture2D texture, out int size)
        {
            if (texture != null)
            {
                int width = texture.width;

                if (ispow2(width) && texture.height == width)
                {
                    size = width;
                    return true;
                }
            }

            size = 0;
            return false;
        }

        private static float SampleBilinearClamp(NativeArray<byte> texture, int2 textureSize, float2 uv)
        {
            uv = (uv * textureSize) - 0.5f;
            var min = (int2)floor(uv);

            // A quick prayer for Burst to SIMD this. 🙏
            int4 index = (clamp(min.y + int4(1, 1, 0, 0), 0, textureSize.y - 1) * textureSize.x) +
                         clamp(min.x + int4(0, 1, 1, 0), 0, textureSize.x - 1);

            float2 t = frac(uv);
            float top = lerp(texture[index.w], texture[index.z], t.x);
            float bottom = lerp(texture[index.x], texture[index.y], t.x);
            return lerp(top, bottom, t.y) * (1f / 255f);
        }
    }

    public delegate float GetHeightDelegate(float x, float z);

    [Serializable]
    public struct GrassPrototype
    {
        [field: SerializeField] public GameObject Source { get; private set; }
        [field: SerializeField] public float Density { get; private set; }
        [field: SerializeField] public float MinScaleXZ { get; private set; }
        [field: SerializeField] public float MaxScaleXZ { get; private set; }
        [field: SerializeField] public float MinScaleY { get; private set; }
        [field: SerializeField] public float MaxScaleY { get; private set; }
        [field: SerializeField] public Mesh Mesh { get; set; }
        [field: SerializeField] public Material Material { get; set; }
    }

    [Serializable]
    public struct FlowerPrototype
    {
        [field: SerializeField] public GameObject Source { get; private set; }
        [field: SerializeField] public float Density { get; private set; }
        [field: SerializeField] public float MinScaleXZ { get; private set; }
        [field: SerializeField] public float MaxScaleXZ { get; private set; }
        [field: SerializeField] public float MinScaleY { get; private set; }
        [field: SerializeField] public float MaxScaleY { get; private set; }
        [field: SerializeField] public Mesh Mesh { get; set; }
        [field: SerializeField] public Material Material { get; set; }
    }
}
