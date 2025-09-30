using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using GPUInstancerPro;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Landscape
{
    public sealed class TreeData : IDisposable
    {
        private readonly int[] instanceCounts;
        private int occupancyFloor;
        private float maxHeight;
        private NativeArray<byte> occupancyMapData;
        private int occupancyMapSize;
        private readonly int[] rendererKeys;
        private readonly TerrainGenerationData terrainData;
        private int2 terrainMinParcel;
        private int2 terrainMaxParcel;
        private int2 treeMinParcel;
        private int2 treeMaxParcel;
        private NativeArray<int> treeIndices;
        private NativeArray<TreeInstanceData> treeInstances;

        public TreeData(int[] rendererKeys, TerrainGenerationData terrainData)
        {
            this.rendererKeys = rendererKeys;
            this.terrainData = terrainData;
            instanceCounts = new int[rendererKeys.Length];
        }

        public void Dispose()
        {
            if (treeIndices.IsCreated)
                treeIndices.Dispose();

            if (treeInstances.IsCreated)
                treeInstances.Dispose();
        }

        public ReadOnlySpan<TreeInstanceData> GetTreeInstances(int2 parcel)
        {
            // If tree data has not been loaded, minParcel == maxParcel, and so this is false, and we
            // don't need to check if treeInstances is empty or anything like that.
            if (parcel.x < treeMinParcel.x || parcel.x >= treeMaxParcel.x
                                           || parcel.y < treeMinParcel.y || parcel.y >= treeMaxParcel.y)
                return ReadOnlySpan<TreeInstanceData>.Empty;

            int index = ((parcel.y - treeMinParcel.y) * (treeMaxParcel.x - treeMinParcel.x))
                + parcel.x - treeMinParcel.x;

            int start = treeIndices[index++];
            int end = index < treeIndices.Length ? treeIndices[index] : treeInstances.Length;
            return treeInstances.AsReadOnlySpan().Slice(start, end - start);
        }

        public bool GetTreeTransform(int2 parcel, TreeInstanceData instance, out Vector3 position,
            out Quaternion rotation, out Vector3 scale)
        {
            position.x = (parcel.x + (instance.PositionX * (1f / 255f))) * terrainData.parcelSize;
            position.z = (parcel.y + (instance.PositionZ * (1f / 255f))) * terrainData.parcelSize;
            LandscapeAsset prototype = terrainData.treeAssets[instance.PrototypeIndex];

            scale = prototype.randomization
                             .LerpScale(float2(instance.ScaleXZ, instance.ScaleY) * (1f / 255f))
                             .xyx;

            if (OverlapsOccupiedParcel(float2(position.x, position.z), prototype.radius * scale.x))
            {
                position.y = 0f;
                rotation = default(Quaternion);
                return false;
            }

            position.y = TerrainGenerator.GetParcelNoiseHeight(position.x, position.z, occupancyMapData,
                occupancyMapSize, terrainData.parcelSize, occupancyFloor, maxHeight);

            rotation = Quaternion.Euler(0f, instance.RotationY * (360f / 255f), 0f);

            return true;
        }

        [Conditional("GPUI_PRO_PRESENT")]
        public void Hide()
        {
            foreach (int rendererKey in rendererKeys)
                GPUICoreAPI.SetInstanceCount(rendererKey, 0);
        }

        [Conditional("GPUI_PRO_PRESENT")]
        public void Instantiate()
        {
            int stride = treeMaxParcel.x - treeMinParcel.x;
            var transforms = new List<Matrix4x4>[terrainData.treeAssets.Length];

            for (int i = 0; i < transforms.Length; i++)
                transforms[i] = new List<Matrix4x4>();

            for (int i = 0; i < treeIndices.Length; i++)
            {
                int2 parcel = int2(i % stride, i / stride) + treeMinParcel;
                ReadOnlySpan<TreeInstanceData> instances = GetTreeInstances(parcel);

                foreach (TreeInstanceData instance in instances)
                {
                    if (GetTreeTransform(parcel, instance, out Vector3 position,
                            out Quaternion rotation, out Vector3 scale))
                    {
                        transforms[instance.PrototypeIndex]
                           .Add(Matrix4x4.TRS(position, rotation, scale));
                    }
                }
            }

            for (int prototypeIndex = 0; prototypeIndex < terrainData.treeAssets.Length;
                 prototypeIndex++)
            {
                List<Matrix4x4> matrices = transforms[prototypeIndex];
                instanceCounts[prototypeIndex] = matrices.Count;

                GPUICoreAPI.SetTransformBufferData(rendererKeys[prototypeIndex],
                    matrices, 0, 0, matrices.Count);

                GPUICoreAPI.SetInstanceCount(rendererKeys[prototypeIndex],
                    instanceCounts[prototypeIndex]);
            }
        }

        private bool IsParcelOccupied(int2 parcel)
        {
            if (any(parcel < terrainMinParcel) || any(parcel > terrainMaxParcel))
                return true; // Treat out of bounds as occupied.

            if (occupancyMapSize <= 0)
                return false;

            parcel += occupancyMapSize / 2;
            int index = (parcel.y * occupancyMapSize) + parcel.x;
            return occupancyMapData[index] == 0;
        }

        public async UniTask LoadAsync(string treeFilePath)
        {
            try
            {
                await using var stream = new FileStream(treeFilePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read);

                using var reader = new BinaryReader(stream, new UTF8Encoding(false), false);

                treeMinParcel.x = reader.ReadInt32();
                treeMinParcel.y = reader.ReadInt32();
                treeMaxParcel.x = reader.ReadInt32();
                treeMaxParcel.y = reader.ReadInt32();

                int2 treeIndexSize = treeMaxParcel - treeMinParcel;

                if (any(treeIndexSize > TerrainGenerator.TERRAIN_SIZE_LIMIT))
                    throw new IOException(
                        $"Tree index size of ({treeIndexSize.x}, {treeIndexSize.y}) exceeds the limit of {TerrainGenerator.TERRAIN_SIZE_LIMIT}");

                treeIndices = new NativeArray<int>(treeIndexSize.x * treeIndexSize.y,
                    Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                ReadReliably(reader, treeIndices);

                int treeInstanceCount = reader.ReadInt32();

                if (treeInstanceCount > TerrainGenerator.TREE_INSTANCE_LIMIT)
                    throw new IOException(
                        $"Tree instance count of {treeInstanceCount} exceeds the limit of {TerrainGenerator.TREE_INSTANCE_LIMIT}");

                treeInstances = new NativeArray<TreeInstanceData>(treeInstanceCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

                ReadReliably(reader, treeInstances);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                    ReportHub.LogWarning(ReportCategory.LANDSCAPE,
                        $"Tree instance data file not found, path: {treeFilePath}");
                else
                    ReportHub.LogException(ex, ReportCategory.LANDSCAPE);

                if (treeIndices.IsCreated)
                    treeIndices.Dispose();

                if (treeInstances.IsCreated)
                    treeInstances.Dispose();

                treeIndices = new NativeArray<int>(0, Allocator.Persistent);
                treeInstances = new NativeArray<TreeInstanceData>(0, Allocator.Persistent);
            }
        }

        private bool OverlapsOccupiedParcel(float2 position, float radius)
        {
            var parcel = (int2)floor(position * (1f / terrainData.parcelSize));

            if (IsParcelOccupied(parcel))
                return true;

            float2 localPosition = position - (parcel * terrainData.parcelSize);

            if (localPosition.x < radius)
            {
                if (IsParcelOccupied(int2(parcel.x - 1, parcel.y)))
                    return true;

                if (localPosition.y < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x - 1, parcel.y - 1)))
                        return true;
                }
            }

            if (terrainData.parcelSize - localPosition.x < radius)
            {
                if (IsParcelOccupied(int2(parcel.x + 1, parcel.y)))
                    return true;

                if (terrainData.parcelSize - localPosition.y < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x + 1, parcel.y + 1)))
                        return true;
                }
            }

            if (localPosition.y < radius)
            {
                if (IsParcelOccupied(int2(parcel.x, parcel.y - 1)))
                    return true;

                if (terrainData.parcelSize - localPosition.x < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x + 1, parcel.y - 1)))
                        return true;
                }
            }

            if (terrainData.parcelSize - localPosition.y < radius)
            {
                if (IsParcelOccupied(int2(parcel.x, parcel.y + 1)))
                    return true;

                if (localPosition.x < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x - 1, parcel.y + 1)))
                        return true;
                }
            }

            return false;
        }

        private static unsafe void ReadReliably<T>(BinaryReader reader, NativeArray<T> array)
            where T: unmanaged
        {
            var buffer = new Span<byte>(array.GetUnsafePtr(), array.Length * sizeof(T));

            while (buffer.Length > 0)
            {
                int read = reader.Read(buffer);

                if (read <= 0)
                    throw new EndOfStreamException("Read zero bytes");

                buffer = buffer.Slice(read);
            }
        }

        public void SetTerrainData(int2 minParcel, int2 maxParcel,
            NativeArray<byte> occupancyMapDataArg, int occupancyMapSizeArg, int occupancyFloorArg, float maxHeightArg)
        {
            terrainMinParcel = minParcel;
            terrainMaxParcel = maxParcel;
            occupancyMapData = occupancyMapDataArg;
            occupancyMapSize = occupancyMapSizeArg;
            occupancyFloor = occupancyFloorArg;
            maxHeight = maxHeightArg;
        }

        [Conditional("GPUI_PRO_PRESENT")]
        public void Show()
        {
            for (int prototypeIndex = 0; prototypeIndex < rendererKeys.Length; prototypeIndex++)
                GPUICoreAPI.SetInstanceCount(rendererKeys[prototypeIndex],
                    instanceCounts[prototypeIndex]);
        }
    }
}
