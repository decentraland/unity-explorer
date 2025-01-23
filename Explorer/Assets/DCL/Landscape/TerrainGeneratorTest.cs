using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using DCL.Profiling;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainGeneratorTest : MonoBehaviour
    {
        public bool clearCache;
        public uint worldSeed = 1;
        public bool digHoles;
        public bool hideTrees;
        public bool hideDetails;
        public bool clearNoiseCacheForWorlds = true;

        public TerrainGenerationData genData;
        public ParcelData parcelData;

        private NativeParallelHashSet<int2> ownedParcels;
        private NativeList<int2> emptyParcels;
        private TerrainGenerator gen;
        private WorldTerrainGenerator wGen;

        private static IMemoryProfiler memoryProfiler;

        private void Start()
        {
            memoryProfiler = new Profiler();
            //GenerateAsync().Forget();
        }

        public void Generate()
        {
            GenerateAsync().Forget();
        }

        private void OnValidate()
        {
            wGen = new WorldTerrainGenerator();
            wGen.Initialize(genData);
        }

        public TerrainGenerator GetGenerator() =>
            gen;

        [ContextMenu(nameof(ClearAppCache))]
        public void ClearAppCache()
        {
            CleanTerrainsCache();
        }

        public void SetUseCache(bool value)
        {
            clearCache = value;
        }

        public void SetHideDetails(bool value)
        {
            hideDetails = value;
        }

        public void SetHideTrees(bool value)
        {
            hideTrees = value;
        }

        [ContextMenu("Generate")]
        public async UniTask GenerateAsync()
        {
            var worldLastTerrain = GameObject.Find("World Generated Terrain");
            if (worldLastTerrain != null) DestroyImmediate(worldLastTerrain);

            var lastTerrain = GameObject.Find("Generated Terrain");
            if (lastTerrain != null) DestroyImmediate(lastTerrain);

            Log("Generate started");
            ownedParcels = parcelData.GetOwnedParcels();
            emptyParcels = parcelData.GetEmptyParcels();

            if (genData.terrainSize == 1)
            {
                if (clearNoiseCacheForWorlds)
                {
                    wGen = new WorldTerrainGenerator();
                    wGen.Initialize(genData);
                }

                await wGen.GenerateTerrainAsync(ownedParcels, worldSeed);
            }
            else
            {
                
                gen = new TerrainGenerator(memoryProfiler, true, clearCache);
                gen.Initialize(genData, ref emptyParcels, ref ownedParcels, "", false);
                await gen.GenerateTerrainAndShowAsync(worldSeed, digHoles, hideTrees, hideDetails);
            }

            LogMemory("Memory after generating terrain is");
            //emptyParcels.Dispose();
            //ownedParcels.Dispose();
            Log("Generate finished");
        }

        private static void LogMemory(string message)
        {
            float afterCleaning = memoryProfiler.SystemUsedMemoryInBytes / (1024 * 1024);
            Debug.Log($"{message} {afterCleaning}MB of memory JUANI");
        }

        public static void CleanTerrainsCache()
        {
            Log("Clearing app cache");

            var deletedFiles = new List<string>();
            var files = Directory.EnumerateFiles(Application.persistentDataPath, "*", SearchOption.TopDirectoryOnly);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                if (fileName.StartsWith("terrain_cache", StringComparison.Ordinal)
                    && fileName.EndsWith(".data", StringComparison.Ordinal))
                {
                    File.Delete(file);
                    deletedFiles.Add(fileName);
                }
            }

            Log($"Clearing app cache finished {deletedFiles.Count}: {string.Join(", ", deletedFiles)}");
        }

        public static void ClearMemory()
        {
            ReportHub.Log(ReportCategory.UNSPECIFIED, "About to clean memory");
            ClearMemoryAsync().Forget();
        }

        private static async UniTask ClearMemoryAsync()
        {
            GC.Collect();
            Resources.UnloadUnusedAssets();
            for (int i = 0; i < 30; i++)
            {
                await UniTask.Yield();
            }

            LogMemory("Cleared memory and now we have");
        }

        private static void Log(string message)
        {
            ReportHub.Log(ReportCategory.LANDSCAPE, message);
        }
    }
}
