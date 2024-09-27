using Cysharp.Threading.Tasks;
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
        private const string DICTIONARY_PATH = "/terrain_cache_dictionaries/";


        public const string ALPHA_MAPS = "alphaMaps";
        public const string HEIGHTS = "heights";
        public const string TREES = "trees";
        public const string DETAIL_LAYER = "detailLayer";
        public const string HOLES = "holes";
        
        
        public static readonly BinaryFormatter FORMATTER = new();

        public int heightX;
        public int heightY;

        public int alphaX;
        public int alphaY;
        public int alphaZ;

        public int detailX;
        public int detailY;

        public int holesX;
        public int holesY;

        public int maxHeight;

        private TerrainLocalCache() { }

        public void SaveMetadataToFile(int seed, int chunkSize, int version, string parcelChecksum)
        {
            var path = GetFilePath(seed, chunkSize, version);
            checksum = parcelChecksum;
            using FileStream fileStream = File.Create(path);
            FORMATTER.Serialize(fileStream, this);
        }

        public void SaveArrayToFile<T>(string name, string offsetX, string offsetZ, T[] arrayToSave) where T : struct
        {
            var pathForDictionary = GetDictionaryFilePath(name, offsetX, offsetZ);
            using var fileStreamForHeights = File.Create(pathForDictionary);
            FORMATTER.Serialize(fileStreamForHeights, arrayToSave);
        }

        public void SaveArrayToFile<T>(string name, string offsetX, string offsetZ, string layer, T[] arrayToSave)
            where T : struct
        {
            var pathForDictionary = GetDictionaryFilePath(name, offsetX, offsetZ, layer);
            using var fileStreamForHeights = File.Create(pathForDictionary);
            FORMATTER.Serialize(fileStreamForHeights, arrayToSave);
        }

        public async UniTask<T[]> RetrieveArrayFromFileAsync<T>(string name, string offsetX, string offsetZ)
        {
            await using var fileStream =
                new FileStream(GetDictionaryFilePath(name, offsetX, offsetZ), FileMode.Open);
            return await UniTask.RunOnThreadPool(() => (T[])FORMATTER.Deserialize(fileStream));
        }

        public async UniTask<T[]> RetrieveArrayFromFileAsync<T>(string name, string offsetX, string offsetZ,
            string layer)
        {
            await using var fileStream =
                new FileStream(GetDictionaryFilePath(name, offsetX, offsetZ, layer), FileMode.Open);
            return await UniTask.RunOnThreadPool(() => (T[])FORMATTER.Deserialize(fileStream));
        }

        private static string GetDictionaryFilePath(string name, string x, string y)
        {
            return GetDictionaryDirectory() + $"{name}_{x}_{y}.data";
        }

        private static string GetDictionaryFilePath(string name, string x, string y, string layer)
        {
            return GetDictionaryDirectory() + $"{name}_{x}_{y}_{layer}.data";
        }

        private static string GetFilePath(int seed, int chunkSize, int version)
        {
            return Application.persistentDataPath + FILE_NAME + $"_{seed}_{chunkSize}_v{version}.data";
        }

        private static string GetDictionaryDirectory()
        {
            return Application.persistentDataPath + DICTIONARY_PATH;
        }

        public static async UniTask<TerrainLocalCache> LoadAsync(int seed, int chunkSize, int version, string parcelChecksum, bool force)
        {
            var emptyCache = new TerrainLocalCache
            {
                checksum = parcelChecksum,
            };

            var path = GetFilePath(seed, chunkSize, version);
            var dictionaryPath = GetDictionaryDirectory();

            if (force && File.Exists(path))
                File.Delete(path);

            if (!File.Exists(path))
            {
                //If the file does not exist, but the dictionary path does, theres a possible corrupt state. 
                //We gotta clear it
                ClearDictionaryPath();
                return emptyCache;
            }

            await using var fileStream = new FileStream(path, FileMode.Open);

            TerrainLocalCache? localCache = await UniTask.RunOnThreadPool(() => (TerrainLocalCache)FORMATTER.Deserialize(fileStream));

            if (localCache.checksum != parcelChecksum)
            {
                File.Delete(path);
                ClearDictionaryPath();
                return emptyCache;
            }

            localCache.isValid = true;
            return localCache;

            void ClearDictionaryPath()
            {
                if (Directory.Exists(dictionaryPath))
                    Directory.Delete(GetDictionaryDirectory(), true);
                Directory.CreateDirectory(dictionaryPath);
            }
        }

        public bool IsValid() =>
            isValid;

    }

    public class TerrainGeneratorLocalCache
    {
        private TerrainLocalCache localCache;
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
            localCache.SaveMetadataToFile(seed, chunkSize, version, parcelChecksum);
        }

        public bool IsValid() =>
            localCache.IsValid();

        public async UniTask<float[,]> GetHeightsAsync(int offsetX, int offsetZ)
        {
            var heightMaps = await localCache.RetrieveArrayFromFileAsync<float>(TerrainLocalCache.HEIGHTS,
                offsetX.ToString(),
                offsetZ.ToString());
            return UnFlatten(heightMaps, localCache.heightX, localCache.heightY);
        }

        public async UniTask<float[,,]> GetAlphaMapsAsync(int offsetX, int offsetZ)
        {
            var alphaMaps = await localCache.RetrieveArrayFromFileAsync<float>(TerrainLocalCache.ALPHA_MAPS,
                offsetX.ToString(),
                offsetZ.ToString());
            return UnFlatten(alphaMaps, localCache.alphaX, localCache.alphaY, localCache.alphaZ);
        }


        public async UniTask<TreeInstance[]> GetTreesAsync(int offsetX, int offsetZ)
        {
            var treesDTO =
                await localCache.RetrieveArrayFromFileAsync<TreeInstanceDTO>(TerrainLocalCache.TREES,
                    offsetX.ToString(),
                    offsetZ.ToString());
            return treesDTO.Select(TreeInstanceDTO.ToOriginal).ToArray();
        }

        public async UniTask<int[,]> GetDetailLayerAsync(int offsetX, int offsetZ, int layer)
        {
            var detailLayer = await localCache.RetrieveArrayFromFileAsync<int>(TerrainLocalCache.DETAIL_LAYER,
                offsetX.ToString(), offsetZ.ToString(), layer.ToString());
            return UnFlatten(detailLayer, localCache.detailX, localCache.detailY);
        }

        public async UniTask<bool[,]> GetHolesAsync(int offsetX, int offsetZ)
        {
            try
            {
                var holesLayer =
                    await localCache.RetrieveArrayFromFileAsync<bool>("holes", offsetX.ToString(), offsetZ.ToString());
                return UnFlatten(holesLayer, localCache.holesX, localCache.holesY);
            }
            catch (Exception e)
            {
                throw new Exception("Cannot get holes from cache. Try to regenerate cache at InfiniteTerrain.scene", e);
            }
        }

        public void SaveHoles(int offsetX, int offsetZ, bool[,] valuePairValue)
        {
            (bool[] array, int row, int col) valueTuple = Flatten(valuePairValue);
            localCache.SaveArrayToFile(TerrainLocalCache.HOLES, offsetX.ToString(), offsetZ.ToString(),
                valueTuple.array);
            localCache.holesX = valueTuple.row;
            localCache.holesY = valueTuple.col;
        }


        public void SaveHeights(int offsetX, int offsetZ, float[,] heightArray)
        {
            (float[] array, int row, int col) valueTuple = Flatten(heightArray);
            localCache.SaveArrayToFile(TerrainLocalCache.HEIGHTS, offsetX.ToString(), offsetZ.ToString(),
                valueTuple.array);
            localCache.heightX = valueTuple.row;
            localCache.heightY = valueTuple.col;
        }

        public void SaveAlphaMaps(int offsetX, int offsetZ, float[,,] alphaMaps)
        {
            (float[] array, int x, int y, int z) valueTuple = Flatten(alphaMaps);
            localCache.SaveArrayToFile(TerrainLocalCache.ALPHA_MAPS, offsetX.ToString(), offsetZ.ToString(),
                valueTuple.array);
            localCache.alphaX = valueTuple.x;
            localCache.alphaY = valueTuple.y;
            localCache.alphaZ = valueTuple.z;
        }

        public void SaveTreeInstances(int offsetX, int offsetZ, TreeInstance[] instances)
        {
            localCache.SaveArrayToFile(TerrainLocalCache.TREES, offsetX.ToString(), offsetZ.ToString(),
                instances.Select(TreeInstanceDTO.Copy).ToArray());
        }

        public void SaveDetailLayer(int offsetX, int offsetZ, int layer, int[,] detailLayer)
        {
            (int[] array, int row, int col) valueTuple = Flatten(detailLayer);
            localCache.SaveArrayToFile(TerrainLocalCache.DETAIL_LAYER, offsetX.ToString(), offsetZ.ToString(),
                layer.ToString(), valueTuple.array);
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
