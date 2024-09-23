﻿using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Utils
{
    [Serializable]
    public struct TreeInstanceDTO
    {
        public float3 position;
        public float widthScale;
        public float heightScale;
        public float rotation;
        public int prototypeIndex;

        public static TreeInstanceDTO Copy(TreeInstance treeInstance) =>
            new ()
            {
                position = treeInstance.position,
                widthScale = treeInstance.widthScale,
                heightScale = treeInstance.heightScale,
                rotation = treeInstance.rotation,
                prototypeIndex = treeInstance.prototypeIndex,
            };

        public static TreeInstance ToOriginal(TreeInstanceDTO dto) =>
            new ()
            {
                position = dto.position,
                widthScale = dto.widthScale,
                heightScale = dto.heightScale,
                rotation = dto.rotation,
                prototypeIndex = dto.prototypeIndex,
            };
    }

    [Serializable]
    public class TerrainLocalCache
    {
        private bool isValid;
        private string checksum;
        private const string FILE_NAME = "/terrain_cache";
        private static readonly BinaryFormatter FORMATTER = new ();

        public Dictionary<int2, float[]> heights = new ();
        public int heightX;
        public int heightY;

        private Dictionary<int2, float[]> alphaMaps = new();
        private int alphaX;
        private int alphaY;
        private int alphaZ;

        public Dictionary<int2, TreeInstanceDTO[]> trees = new ();

        public Dictionary<int3, int[]> detail = new ();
        public int detailX;
        public int detailY;

        public Dictionary<int2, bool[]> holes = new ();
        public int holesX;
        public int holesY;

        public int maxHeight;

        private TerrainLocalCache() { }

        public void SaveToFile(int seed, int chunkSize, int version, string parcelChecksum)
        {
            string path = GetPath(seed, chunkSize, version);
            checksum = parcelChecksum;

            if (File.Exists(path))
                File.Delete(path);

            using FileStream fileStream = File.Create(path);
            FORMATTER.Serialize(fileStream, this);
        }

        private static string GetPath(int seed, int chunkSize, int version) =>
            Application.persistentDataPath + FILE_NAME + $"_{seed}_{chunkSize}_v{version}.data";

        public static TerrainLocalCache NewEmpty() =>
            new()
            {
                isValid = false
            };

        public static async UniTask<TerrainLocalCache> LoadAsync(int seed, int chunkSize, int version, string parcelChecksum, bool force)
        {
            var emptyCache = new TerrainLocalCache
            {
                checksum = parcelChecksum,
            };

            string path = GetPath(seed, chunkSize, version);

            if (force && File.Exists(path))
                File.Delete(path);

            if (!File.Exists(path))
                return emptyCache;

            await using var fileStream = new FileStream(path, FileMode.Open);

            TerrainLocalCache? localCache = await UniTask.RunOnThreadPool(() => (TerrainLocalCache)FORMATTER.Deserialize(fileStream));

            if (localCache.checksum != parcelChecksum)
                return emptyCache;

            localCache.isValid = true;
            return localCache;
        }

        public bool IsValid() =>
            isValid;

        public void Dispose()
        {
            Debug.Log("JUANI CALLING THE DISPOSE");
            Debug.Log($"JUANI ALPHA MAPS LENGTH {alphaMaps.Count}");

            GC.WaitForPendingFinalizers();
            Resources.UnloadUnusedAssets();
        }

        public void SaveAlphaMap(int offsetX, int offsetZ, (float[] array, int x, int y, int z) valueTuple)
        {
            alphaMaps.Add(new int2(offsetX, offsetZ), valueTuple.array);
            Debug.Log($"JUANI SIZE OF THE ARRAY {valueTuple.array.Count()}");
            alphaX = valueTuple.x;
            alphaY = valueTuple.y;
            alphaZ = valueTuple.z;
        }
    }

    public class TerrainGeneratorLocalCache
    {
        private TerrainLocalCache localCache = TerrainLocalCache.NewEmpty();
        private readonly int seed;
        private readonly int chunkSize;
        private readonly int version;
        private readonly string parcelChecksum;

        public TerrainGeneratorLocalCache(int seed, int chunkSize, int version, string parcelChecksum)
        {
            this.seed = seed;
            this.chunkSize = chunkSize;
            this.version = version;
            this.parcelChecksum = parcelChecksum;
        }

        public async UniTask LoadAsync(bool force)
        {
            localCache = await TerrainLocalCache.LoadAsync(seed, chunkSize, version, parcelChecksum, force);
            ReportHub.Log(ReportCategory.LANDSCAPE, "Landscape cache loaded and its validity status is: " + localCache.IsValid());
        }

        public void Save()
        {
            localCache.SaveToFile(seed, chunkSize, version, parcelChecksum);
            localCache.Dispose();
            return;
            localCache.heights.Clear();
            localCache.detail.Clear();
            localCache.trees.Clear();
            localCache.holes.Clear();
            localCache = TerrainLocalCache.NewEmpty();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public bool IsValid() =>
            localCache.IsValid();

        public float[,] GetHeights(int offsetX, int offsetZ) =>
            UnFlatten(localCache.heights[new int2(offsetX, offsetZ)], localCache.heightX, localCache.heightY);

        public float[,,] GetAlphaMaps(int offsetX, int offsetZ) =>
            new float[0, 0, 0];
        //UnFlatten(localCache.alphaMaps[new int2(offsetX, offsetZ)], localCache.alphaX, localCache.alphaY, localCache.alphaZ);

        public TreeInstance[] GetTrees(int offsetX, int offsetZ)
        {
            TreeInstance[] treeInstances = localCache.trees[new int2(offsetX, offsetZ)].Select(TreeInstanceDTO.ToOriginal).ToArray();
            return treeInstances;
        }

        public int[,] GetDetailLayer(int offsetX, int offsetZ, int layer) =>
            UnFlatten(localCache.detail[new int3(offsetX, offsetZ, layer)], localCache.detailX, localCache.detailY);

        public bool[,] GetHoles(int offsetX, int offsetZ)
        {
            try { return UnFlatten(localCache.holes[new int2(offsetX, offsetZ)], localCache.holesX, localCache.holesY); }
            catch (KeyNotFoundException e) { throw new Exception("Cannot get holes from cache. Try to regenerate cache at InfiniteTerrain.scene", e); }
        }

        public void SaveHoles(int offsetX, int offsetZ, bool[,] valuePairValue)
        {
            (bool[] array, int row, int col) valueTuple = Flatten(valuePairValue);
            localCache.holes.Add(new int2(offsetX, offsetZ), valueTuple.array);
            localCache.holesX = valueTuple.row;
            localCache.holesY = valueTuple.col;
        }

        public void SaveHeights(int offsetX, int offsetZ, float[,] heightArray)
        {
            (float[] array, int row, int col) valueTuple = Flatten(heightArray);
            localCache.heights.Add(new int2(offsetX, offsetZ), valueTuple.array);
            localCache.heightX = valueTuple.row;
            localCache.heightY = valueTuple.col;
        }

        public void SaveAlphaMaps(int offsetX, int offsetZ, float[,,] alphaMaps)
        {
            (float[] array, int x, int y, int z) valueTuple = Flatten(alphaMaps);
            localCache.SaveAlphaMap(offsetX, offsetZ, valueTuple);
        }

        public void SaveTreeInstances(int offsetX, int offsetZ, TreeInstance[] instances)
        {
            localCache.trees.Add(new int2(offsetX, offsetZ), instances.Select(TreeInstanceDTO.Copy).ToArray());
        }

        public void SaveDetailLayer(int offsetX, int offsetZ, int layer, int[,] detailLayer)
        {
            (int[] array, int row, int col) valueTuple = Flatten(detailLayer);
            localCache.detail.Add(new int3(offsetX, offsetZ, layer), valueTuple.array);
            localCache.detailX = valueTuple.row;
            localCache.detailY = valueTuple.col;
        }

        public int GetMaxHeight() =>
            localCache.maxHeight;

        public void SetMaxHeight(int maxHeightIndex)
        {
            localCache.maxHeight = maxHeightIndex;
        }

        private static (T[] array, int row, int col) Flatten<T>(T[,] array)
        {
            int rowCount = array.GetLength(0);
            int colCount = array.GetLength(1);

            var flattenedArray = new T[rowCount * colCount];

            for (var i = 0; i < rowCount; i++)
            for (var j = 0; j < colCount; j++)
            {
                int index = (i * colCount) + j;
                flattenedArray[index] = array[i, j];
            }

            return (flattenedArray, rowCount, colCount);
        }

        private static (T[] array, int x, int y, int z) Flatten<T>(T[,,] array)
        {
            int dim1 = array.GetLength(0);
            int dim2 = array.GetLength(1);
            int dim3 = array.GetLength(2);

            var flattenedArray = new T[dim1 * dim2 * dim3];

            for (var i = 0; i < dim1; i++)
            for (var j = 0; j < dim2; j++)
            for (var k = 0; k < dim3; k++)
            {
                int index = (i * dim2 * dim3) + (j * dim3) + k;
                flattenedArray[index] = array[i, j, k];
            }

            return (flattenedArray, dim1, dim2, dim3);
        }

        private static T[,] UnFlatten<T>(IReadOnlyList<T> flattenedArray, int rowCount, int colCount)
        {
            var array = new T[rowCount, colCount];

            for (var i = 0; i < rowCount; i++)
            for (var j = 0; j < colCount; j++)
            {
                int index = (i * colCount) + j;
                array[i, j] = flattenedArray[index];
            }

            return array;
        }

        private static T[,,] UnFlatten<T>(IReadOnlyList<T> flattenedArray, int dim1, int dim2, int dim3)
        {
            var array = new T[dim1, dim2, dim3];

            for (var i = 0; i < dim1; i++)
            for (var j = 0; j < dim2; j++)
            for (var k = 0; k < dim3; k++)
            {
                int index = (i * dim2 * dim3) + (j * dim3) + k;
                array[i, j, k] = flattenedArray[index];
            }

            return array;
        }
    }
}
