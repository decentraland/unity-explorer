using DCL.Landscape.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Landscape
{
    public sealed class TreeInstanceWriter
    {
        private int2 minParcel;
        private int2 maxParcel;
        private readonly Dictionary<int2, List<TreeInstanceData>> parcelMap;
        private readonly int parcelSize;
        private readonly LandscapeAsset[] prototypes;
        private int instanceCount;

        public TreeInstanceWriter(int parcelSize, LandscapeAsset[] prototypes)
        {
            minParcel = int2(int.MaxValue, int.MaxValue);
            maxParcel = int2(int.MinValue, int.MinValue);
            parcelMap = new Dictionary<int2, List<TreeInstanceData>>();
            this.parcelSize = parcelSize;
            this.prototypes = prototypes;
        }

        public void AddTerrain(Terrain unityTerrain)
        {
            var terrainData = unityTerrain.terrainData;
            var treeInstances = terrainData.treeInstances;

            Matrix4x4 unityTerrainToWorld = unityTerrain.transform.localToWorldMatrix
                                            * Matrix4x4.Scale(terrainData.size);

            foreach (var treeInstance in treeInstances)
            {
                Vector3 position = unityTerrainToWorld.MultiplyPoint3x4(treeInstance.position);
                int2 parcel = (int2)floor(float3(position).xz / parcelSize);

                if (!parcelMap.TryGetValue(parcel, out List<TreeInstanceData> treeInstancesInParcel))
                {
                    treeInstancesInParcel = new();
                    parcelMap.Add(parcel, treeInstancesInParcel);
                    minParcel = min(minParcel, parcel);
                    maxParcel = max(maxParcel, parcel);
                }

                treeInstancesInParcel.Add(new TreeInstanceData(treeInstance, position, parcelSize,
                    prototypes));

                instanceCount++;
            }
        }

        public void Write(Stream stream)
        {
            int parcelStart = 0;
            int2 terrainSize = maxParcel - minParcel + 1;
            int[] treeIndices = new int[terrainSize.x * terrainSize.y];
            TreeInstanceData[] treeInstances = new TreeInstanceData[instanceCount];

            for (int z = minParcel.y; z <= maxParcel.y; z++)
            for (int x = minParcel.x; x <= maxParcel.x; x++)
            {
                treeIndices[((z - minParcel.y) * terrainSize.x) + x - minParcel.x] = parcelStart;

                if (parcelMap.TryGetValue(int2(x, z), out List<TreeInstanceData> treeInstancesInParcel))
                {
                    treeInstancesInParcel.CopyTo(treeInstances, parcelStart);
                    parcelStart += treeInstancesInParcel.Count;
                }
            }

            using var writer = new BinaryWriter(stream, new UTF8Encoding(false), true);

            unsafe
            {
                writer.Write(minParcel.x);
                writer.Write(minParcel.y);
                writer.Write(maxParcel.x + 1);
                writer.Write(maxParcel.y + 1);

                fixed (int* treeIndicesPtr = treeIndices)
                {
                    writer.Write(new ReadOnlySpan<byte>(
                        (byte*)treeIndicesPtr, treeIndices.Length * sizeof(int)));
                }

                writer.Write(instanceCount);

                fixed (TreeInstanceData* treeInstancesPtr = treeInstances)
                {
                    writer.Write(new ReadOnlySpan<byte>(
                        (byte*)treeInstancesPtr, treeInstances.Length * sizeof(TreeInstanceData)));
                }
            }
        }
    }
}
