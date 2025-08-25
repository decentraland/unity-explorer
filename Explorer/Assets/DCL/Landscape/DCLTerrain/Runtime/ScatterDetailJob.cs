using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    internal struct ScatterDetailJob : IJobParallelFor
    {
        public TerrainDataData TerrainData;
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<DetailPrototypeData> Prototypes;
        public float3 CameraPosition;
        [ReadOnly] public ClipVolume CameraFrustum;
        public int2 RectMin;
        public int RectSizeX;
        public NativeList<DetailInstanceData>.ParallelWriter Instances;

        public void Execute(int index)
        {
            int2 parcel = int2(index % RectSizeX, index / RectSizeX) + RectMin;

            if (TerrainData.IsOccupied(parcel))
                return;

            if (!CameraFrustum.Overlaps(TerrainData.GetParcelBounds(parcel)))
                return;

            Random random = TerrainData.GetRandom(parcel);

            // TODO: Support more than one detail prototype and have more than one way to scatter
            // instances.
            JitteredGrid(parcel, 0, ref random, Instances);
        }

        private bool JitteredGrid(int2 parcel, int meshIndex,
            ref Random random, NativeList<DetailInstanceData>.ParallelWriter instances)
        {
            DetailPrototypeData prototype = Prototypes[meshIndex];
            var gridSize = (int)(prototype.Density * TerrainData.ParcelSize);
            float invGridSize = (float)TerrainData.ParcelSize / gridSize;
            float2 corner0 = parcel * TerrainData.ParcelSize;

            int xMin = TerrainData.IsOccupied(int2(parcel.x - 1, parcel.y)) ? 1 : 0;
            int xMax = gridSize - (TerrainData.IsOccupied(int2(parcel.x + 1, parcel.y)) ? 1 : 0);
            int zMin = TerrainData.IsOccupied(int2(parcel.x, parcel.y - 1)) ? 1 : 0;
            int zMax = gridSize - (TerrainData.IsOccupied(int2(parcel.x, parcel.y + 1)) ? 1 : 0);

            for (int z = zMin; z < zMax; z++)
            for (int x = xMin; x < xMax; x++)
            {
                float3 position;
                position.x = corner0.x + (x * invGridSize) + random.NextFloat(invGridSize);
                position.z = corner0.y + (z * invGridSize) + random.NextFloat(invGridSize);
                position.y = TerrainData.GetHeight(position.x, position.z);

                float rotationY = random.NextFloat(-180f, 180f);

                float scaleXZ = random.NextFloat(prototype.MinScaleXZ, prototype.MaxScaleXZ);
                float scaleY = random.NextFloat(prototype.MinScaleY, prototype.MaxScaleY);

                var instance = new DetailInstanceData
                {
                    MeshIndex = meshIndex,
                    Position = position,
                    RotationY = rotationY,
                    ScaleXZ = scaleXZ,
                    ScaleY = scaleY,
                };

                if (!instances.TryAddNoResize(instance))
                    return false;
            }

            return true;
        }
    }
}
