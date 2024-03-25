using Cysharp.Threading.Tasks;
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
        private const string FILE_NAME = "/terrain_cache";

        public Dictionary<int2, float[]> heights = new ();
        public int heightX;
        public int heightY;

        public Dictionary<int2, float[]> alphaMaps = new ();
        public int alphaX;
        public int alphaY;
        public int alphaZ;

        public Dictionary<int2, TreeInstanceDTO[]> trees = new ();

        public Dictionary<int3, int[]> detail = new ();
        public int detailX;
        public int detailY;

        public Dictionary<int2, bool[]> holes = new ();
        public int holesX;
        public int holesY;

        public int maxHeight;

        private TerrainLocalCache() { }

        public void SaveToFile(int seed, int chunkSize, int version)
        {
            string path = GetPath(seed, chunkSize, version);

            if (File.Exists(path))
                File.Delete(path);

            var formatter = new BinaryFormatter();

            using FileStream fileStream = File.Create(path);
            formatter.Serialize(fileStream, this);
        }

        private static string GetPath(int seed, int chunkSize, int version) =>
            Application.persistentDataPath + FILE_NAME + $"_{seed}_{chunkSize}_v{version}.data";

        public static async UniTask<TerrainLocalCache> LoadAsync(int seed, int chunkSize, int version, bool force)
        {
            var localCache = new TerrainLocalCache();

            string path = GetPath(seed, chunkSize, version);

            if (force && File.Exists(path))
                File.Delete(path);

            if (!File.Exists(path))
                return localCache;

            await using (var fileStream = new FileStream(path, FileMode.Open))
                localCache = await UniTask.RunOnThreadPool(() =>
                {
                    var formatter = new BinaryFormatter();
                    return (TerrainLocalCache)formatter.Deserialize(fileStream);
                });

            localCache.isValid = true;

            return localCache;
        }

        public bool IsValid() =>
            isValid;
    }

    public class TerrainGeneratorLocalCache
    {
        private TerrainLocalCache localCache;
        private readonly bool isValid;
        private readonly int seed;
        private readonly int chunkSize;
        private readonly int version;

        public TerrainGeneratorLocalCache(int seed, int chunkSize, int version)
        {
            this.seed = seed;
            this.chunkSize = chunkSize;
            this.version = version;
        }

        public async UniTask LoadAsync(bool force)
        {
            localCache = await TerrainLocalCache.LoadAsync(seed, chunkSize, version, force);
        }

        public void Save()
        {
            localCache.SaveToFile(seed, chunkSize, version);
        }

        public bool IsValid() =>
            localCache.IsValid();

        public float[,] GetHeights(int offsetX, int offsetZ) =>
            UnFlatten(localCache.heights[new int2(offsetX, offsetZ)], localCache.heightX, localCache.heightY);

        public float[,,] GetAlphaMaps(int offsetX, int offsetZ) =>
            UnFlatten(localCache.alphaMaps[new int2(offsetX, offsetZ)], localCache.alphaX, localCache.alphaY, localCache.alphaZ);

        public TreeInstance[] GetTrees(int offsetX, int offsetZ)
        {
            TreeInstance[] treeInstances = localCache.trees[new int2(offsetX, offsetZ)].Select(TreeInstanceDTO.ToOriginal).ToArray();
            return treeInstances;
        }

        public int[,] GetDetailLayer(int offsetX, int offsetZ, int layer) =>
            UnFlatten(localCache.detail[new int3(offsetX, offsetZ, layer)], localCache.detailX, localCache.detailY);

        public bool[,] GetHoles(int offsetX, int offsetZ) =>
            UnFlatten(localCache.holes[new int2(offsetX, offsetZ)], localCache.holesX, localCache.holesY);

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
            localCache.alphaMaps.Add(new int2(offsetX, offsetZ), valueTuple.array);
            localCache.alphaX = valueTuple.x;
            localCache.alphaY = valueTuple.y;
            localCache.alphaZ = valueTuple.z;
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
