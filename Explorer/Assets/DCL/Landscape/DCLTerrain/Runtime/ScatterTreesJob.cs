using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    [BurstCompile]
    internal struct ScatterTreesJob : IJobParallelFor
    {
        public TerrainDataData TerrainData;
        public float3 CameraPosition;
        [ReadOnly] public ClipVolume CameraFrustum;
        public int2 RectMin;
        public int RectSizeX;
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<TreeLODData> Lods;
        public NativeList<TreeInstanceData>.ParallelWriter Instances;

        public void Execute(int index)
        {
            int2 parcel = int2(index % RectSizeX, index / RectSizeX) + RectMin;

            if (TerrainData.IsOccupied(parcel))
                return;

            if (!CameraFrustum.Overlaps(TerrainData.GetParcelBounds(parcel)))
                return;

            ReadOnlySpan<TreeInstance> instances = TerrainData.GetTreeInstances(parcel);

            for (var i = 0; i < instances.Length; i++)
            {
                if (!TerrainData.TryGenerateTree(parcel, instances[i], out float3 position,
                        out float rotationY, out float scaleXZ, out float scaleY)) { continue; }

                int prototypeIndex = instances[i].PrototypeIndex;
                TreePrototypeData prototype = TerrainData.TreePrototypes[prototypeIndex];
                float screenSize = prototype.LocalSize / distance(position, CameraPosition);
                int meshIndex = prototype.Lod0MeshIndex;

                prototypeIndex++;

                int meshEnd = prototypeIndex < TerrainData.TreePrototypes.Length
                    ? TerrainData.TreePrototypes[prototypeIndex].Lod0MeshIndex
                    : Lods.Length;

                while (meshIndex < meshEnd && Lods[meshIndex].MinScreenSize > screenSize)
                    meshIndex++;

                if (meshIndex < meshEnd)
                {
                    var instance = new TreeInstanceData
                    {
                        MeshIndex = meshIndex,
                        Position = position,
                        RotationY = rotationY,
                        ScaleXZ = scaleXZ,
                        ScaleY = scaleY,
                    };

                    if (!Instances.TryAddNoResize(instance))
                        break;
                }
            }
        }
    }
}
