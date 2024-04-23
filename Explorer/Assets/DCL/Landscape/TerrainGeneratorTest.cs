using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using System;
using System.Collections.Generic;
using System.IO;
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

        private void Start()
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

        [ContextMenu("Generate")]
        public async UniTask GenerateAsync()
        {
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
                gen = new TerrainGenerator(true, clearCache);
                gen.Initialize(genData, ref emptyParcels, ref ownedParcels);
                await gen.GenerateTerrainAsync(worldSeed, digHoles, hideTrees, hideDetails, true);
            }

            emptyParcels.Dispose();
            ownedParcels.Dispose();
            Log("Generate finished");
        }

        private static void Log(string message)
        {
            ReportHub.Log(ReportData.UNSPECIFIED, message);
        }
    }
}
