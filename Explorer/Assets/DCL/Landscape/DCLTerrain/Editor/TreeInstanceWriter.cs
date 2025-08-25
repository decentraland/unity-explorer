#if TERRAIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Decentraland.Terrain
{
    public sealed class TreeInstanceWriter
    {
        private int2 minParcel;
        private int2 maxParcel;
        private readonly Dictionary<int2, List<TreeInstance>> parcelMap;
        private readonly int parcelSize;
        private readonly TreePrototype[] prototypes;
        private int treeInstanceCount;

        public TreeInstanceWriter(int parcelSize, TreePrototype[] prototypes)
        {
            minParcel = int2(int.MaxValue, int.MaxValue);
            maxParcel = int2(int.MinValue, int.MinValue);
            parcelMap = new Dictionary<int2, List<TreeInstance>>();
            this.parcelSize = parcelSize;
            this.prototypes = prototypes;
        }

        public void AddTerrain(UnityEngine.Terrain unityTerrain)
        {
            UnityEngine.TerrainData terrainData = unityTerrain.terrainData;
            UnityEngine.TreeInstance[] treeInstances = terrainData.treeInstances;

            Matrix4x4 unityTerrainToWorld = unityTerrain.transform.localToWorldMatrix
                                            * Matrix4x4.Scale(terrainData.size);

            foreach (UnityEngine.TreeInstance treeInstance in treeInstances)
            {
                Vector3 position = unityTerrainToWorld.MultiplyPoint3x4(treeInstance.position);
                var parcel = (int2)floor(position.XZ() / parcelSize);

                if (!parcelMap.TryGetValue(parcel, out List<TreeInstance> treeInstancesInParcel))
                {
                    treeInstancesInParcel = new List<TreeInstance>();
                    parcelMap.Add(parcel, treeInstancesInParcel);
                    minParcel = min(minParcel, parcel);
                    maxParcel = max(maxParcel, parcel);
                }

                treeInstancesInParcel.Add(new TreeInstance(treeInstance, position, parcelSize,
                    prototypes));

                treeInstanceCount++;
            }
        }

        public void Write(Stream stream)
        {
            var parcelStart = 0;
            int2 terrainSize = maxParcel - minParcel + 1;
            var treeIndices = new int[terrainSize.x * terrainSize.y];
            var treeInstances = new TreeInstance[treeInstanceCount];

            for (int z = minParcel.y; z <= maxParcel.y; z++)
            for (int x = minParcel.x; x <= maxParcel.x; x++)
            {
                treeIndices[((z - minParcel.y) * terrainSize.x) + x - minParcel.x] = parcelStart;

                if (parcelMap.TryGetValue(int2(x, z), out List<TreeInstance> treeInstancesInParcel))
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
                    writer.Write(new ReadOnlySpan<byte>(
                        (byte*)treeIndicesPtr, treeIndices.Length * sizeof(int)));

                writer.Write(treeInstanceCount);

                fixed (TreeInstance* treeInstancesPtr = treeInstances)
                    writer.Write(new ReadOnlySpan<byte>(
                        (byte*)treeInstancesPtr, treeInstances.Length * sizeof(TreeInstance)));
            }
        }
    }
}
#endif
