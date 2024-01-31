using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;
using Debug = UnityEngine.Debug;
using JobHandle = Unity.Jobs.JobHandle;
using Object = UnityEngine.Object;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape
{
    public class TerrainGenerator : ITerrainGenerator, IDisposable
    {
        private readonly TerrainGenerationData terrainGenData;
        private GameObject rootGo;
        private TreePrototype[] treePrototypes;
        private NativeHashMap<int2, EmptyParcelData> emptyParcelResult;
        private NativeArray<int2> emptyParcels;
        private NativeHashSet<int2> ownedParcels;
        private int maxHeightIndex;
        private Random random;
        private readonly NoiseGeneratorCache noiseGenCache;
        private bool hideTrees;
        private bool hideDetails;
        private readonly ReportData reportData;

        public TerrainGenerator(TerrainGenerationData terrainGenData, ref NativeArray<int2> emptyParcels, ref NativeHashSet<int2> ownedParcels)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
            noiseGenCache = new NoiseGeneratorCache();
            reportData = new ReportData("TERRAIN_GENERATOR");
        }

        public async UniTask GenerateTerrain(uint worldSeed = 1, bool withHoles = true, bool centerTerrain = true, bool hideTrees = false, bool hideDetails = false,
            AsyncLoadProcessReport processReport = null)
        {
            try
            {
                var time = new Stopwatch();
                time.Start();
                this.hideDetails = hideDetails;
                this.hideTrees = hideTrees;
                random = new Random((uint)terrainGenData.seed);

                await SetupEmptyParcelData();

                if (processReport != null) processReport.ProgressCounter.Value = 0.1f;

                rootGo = GameObject.Find("Generated Terrain");
                if (rootGo != null) Object.DestroyImmediate(rootGo);
                rootGo = new GameObject("Generated Terrain");

                await SpawnMisc();

                GenerateCliffs();

                if (centerTerrain)
                    rootGo.transform.position = new Vector3(-terrainGenData.terrainSize / 2f, 0, -terrainGenData.terrainSize / 2f);

                Dictionary<int2, TerrainData> terrainDatas = new ();

                float total = Mathf.Pow(Mathf.CeilToInt(terrainGenData.terrainSize / (float)terrainGenData.chunkSize), 2);
                var progress = 0;

                for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
                for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
                {
                    progress++;
                    terrainDatas.Add(new int2(x, z), await GenerateTerrainData(x, z, worldSeed));

                    if (processReport != null) processReport.ProgressCounter.Value = 0.1f + (progress / total * 0.7f);
                }

                if (withHoles)
                    DigHoles(terrainDatas);

                if (processReport != null) processReport.ProgressCounter.Value = 0.9f;

                GenerateChunks(terrainDatas);

                if (processReport != null) processReport.ProgressCounter.Value = 1f;

                time.Stop();
                ReportHub.Log(LogType.Log, reportData, $"Terrain generation was done in {time.ElapsedMilliseconds / 1000f:F2} seconds");
            }
            catch (Exception e) { ReportHub.LogException(e, reportData); }
        }

        private async UniTask SpawnMisc()
        {
            Transform ocean = Object.Instantiate(terrainGenData.ocean).transform;
            ocean.SetParent(rootGo.transform, true);

            Transform wind = Object.Instantiate(terrainGenData.wind).transform;
            wind.SetParent(rootGo.transform, true);
        }

        //           size,size
        //      N
        //    W + E
        //      S
        //0,0

        private void GenerateCliffs()
        {
            if (terrainGenData.cliffSide == null || terrainGenData.cliffCorner == null)
                return;

            CreateCliffCornerAt(new Vector3(terrainGenData.terrainSize, 0, terrainGenData.terrainSize), Quaternion.identity);
            CreateCliffCornerAt(new Vector3(terrainGenData.terrainSize, 0, 0), Quaternion.Euler(0, 90, 0));
            CreateCliffCornerAt(new Vector3(0, 0, 0), Quaternion.Euler(0, 180, 0));
            CreateCliffCornerAt(new Vector3(0, 0, terrainGenData.terrainSize), Quaternion.Euler(0, 270, 0));

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(terrainGenData.terrainSize, 0, i + 16);
                side.rotation = Quaternion.Euler(0, 90, 0);
                side.SetParent(rootGo.transform, true);
            }

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(i, 0, terrainGenData.terrainSize);
                side.rotation = Quaternion.identity;
                side.SetParent(rootGo.transform, true);
            }

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(0, 0, i);
                side.rotation = Quaternion.Euler(0, 270, 0);
                side.SetParent(rootGo.transform, true);
            }

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(i + 16, 0, 0);
                side.rotation = Quaternion.Euler(0, 180, 0);
                side.SetParent(rootGo.transform, true);
            }
        }

        private void CreateCliffCornerAt(Vector3 position, Quaternion rotation)
        {
            Transform neCorner = Object.Instantiate(terrainGenData.cliffCorner).transform;
            neCorner.position = position;
            neCorner.rotation = rotation;
            neCorner.SetParent(rootGo.transform, true);
        }

        private async UniTask SetupEmptyParcelData()
        {
            emptyParcelResult = new NativeHashMap<int2, EmptyParcelData>(emptyParcels.Length, Allocator.Persistent);

            foreach (int2 emptyParcel in emptyParcels)
                emptyParcelResult.Add(new int2(emptyParcel.x, emptyParcel.y), new EmptyParcelData());

            var job = new SetupEmptyParcels(in emptyParcels, in ownedParcels, ref emptyParcelResult, terrainGenData.heightScaleNerf);
            JobHandle handle = job.Schedule();

            await handle.ToUniTask(PlayerLoopTiming.Update);

            maxHeightIndex = 0;

            foreach (KVPair<int2, EmptyParcelData> pair in emptyParcelResult)
                if (pair.Value.minIndex > maxHeightIndex)
                    maxHeightIndex = pair.Value.minIndex;
        }

        private void GenerateChunks(Dictionary<int2, TerrainData> terrainDatas)
        {
            for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
            for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
            {
                TerrainData terrainData = terrainDatas[new int2(x, z)];
                GenerateTerrainChunk(x, z, terrainData, terrainGenData.terrainMaterial);
            }
        }

        private void DigHoles(Dictionary<int2, TerrainData> terrainDatas)
        {
            int resolution = terrainGenData.chunkSize;
            var failedHoles = 0;
            var goodHoles = 0;

            var parcelSizeHole = new bool[16, 16];

            for (var i = 0; i < 16; i++)
            for (var j = 0; j < 16; j++)
                parcelSizeHole[i, j] = false;

            foreach (int2 ownedParcel in ownedParcels)
            {
                int parcelGlobalX = (150 + ownedParcel.x) * 16;
                int parcelGlobalY = (150 + ownedParcel.y) * 16;

                // Calculate the terrain chunk index for the parcel
                int chunkX = Mathf.FloorToInt((float)parcelGlobalX / resolution);
                int chunkY = Mathf.FloorToInt((float)parcelGlobalY / resolution);

                // Calculate the position within the terrain chunk
                int localX = parcelGlobalX - (chunkX * resolution);
                int localY = parcelGlobalY - (chunkY * resolution);

                try
                {
                    TerrainData terrainData = terrainDatas[new int2(chunkX * resolution, chunkY * resolution)];
                    terrainData.SetHoles(localX, localY, parcelSizeHole);
                    goodHoles++;
                }
                catch (Exception e) { failedHoles++; }
            }

            if (failedHoles > 0)
                ReportHub.LogError(reportData, $"Failed to set {failedHoles} holes");
        }

        private void GenerateTerrainChunk(int offsetX, int offsetZ, TerrainData terrainData, Material material)
        {
            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.materialTemplate = material;
            terrain.detailObjectDistance = 200;
            terrain.enableHeightmapRayTracing = false;
            terrainObject.transform.position = new Vector3(offsetX, -terrainGenData.minHeight, offsetZ);
            terrainObject.transform.SetParent(rootGo.transform, false);
        }

        private async UniTask<TerrainData> GenerateTerrainData(int offsetX, int offsetZ, uint baseSeed)
        {
            int resolution = terrainGenData.chunkSize;
            int chunkSize = terrainGenData.chunkSize;

            var terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                alphamapResolution = resolution,
                size = new Vector3(chunkSize, maxHeightIndex, chunkSize),
                terrainLayers = terrainGenData.terrainLayers,
                treePrototypes = GetTreePrototypes(),
                detailPrototypes = GetDetailPrototypes(),
            };

            terrainData.SetDetailResolution(chunkSize, 32);

            await SetHeights(offsetX, offsetZ, terrainData, baseSeed);
            await SetTextures(offsetX, offsetZ, resolution, terrainData, baseSeed);

            if (!hideTrees) await SetTrees(offsetX, offsetZ, chunkSize, terrainData, baseSeed);
            if (!hideDetails) await SetDetails(offsetX, offsetZ, chunkSize, terrainData, baseSeed);

            return terrainData;
        }

        private DetailPrototype[] GetDetailPrototypes()
        {
            return terrainGenData.detailAssets.Select(a =>
                                  {
                                      var detailPrototype = new DetailPrototype
                                      {
                                          usePrototypeMesh = true,
                                          prototype = a.asset,
                                          useInstancing = true,
                                          renderMode = DetailRenderMode.VertexLit,
                                          density = a.TerrainDetailSettings.detailDensity,
                                          alignToGround = a.TerrainDetailSettings.alignToGround / 100f,
                                          holeEdgePadding = a.TerrainDetailSettings.holeEdgePadding / 100f,
                                          minWidth = a.TerrainDetailSettings.minWidth,
                                          maxWidth = a.TerrainDetailSettings.maxWidth,
                                          minHeight = a.TerrainDetailSettings.minHeight,
                                          maxHeight = a.TerrainDetailSettings.maxHeight,
                                          noiseSeed = a.TerrainDetailSettings.noiseSeed,
                                          noiseSpread = a.TerrainDetailSettings.noiseSpread,
                                          useDensityScaling = a.TerrainDetailSettings.affectedByGlobalDensityScale,
                                          positionJitter = a.TerrainDetailSettings.positionJitter / 100f,
                                      };
                                      return detailPrototype;
                                  })
                                 .ToArray();
        }

        private async UniTask SetDetails(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed)
        {
            terrainData.SetDetailScatterMode(terrainGenData.detailScatterMode);

            int detailSize = chunkSize;

            for (int i = 0; i < terrainGenData.detailAssets.Length; i++)
            {
                LandscapeAsset detailAsset = terrainGenData.detailAssets[i];

                try
                {
                    int[,] detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, i);

                    INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(detailAsset.noiseData, baseSeed);
                    JobHandle handle = noiseGenerator.Schedule(detailSize, offsetX, offsetZ);
                    await handle.ToUniTask(PlayerLoopTiming.Update);

                    for (var y = 0; y < detailSize; y++)
                    {
                        for (var x = 0; x < detailSize; x++)
                        {
                            int f = terrainGenData.detailScatterMode == DetailScatterMode.CoverageMode ? 255 : 16;
                            int index = x + (y * detailSize);
                            float value = noiseGenerator.GetValue(index);
                            detailLayer[y, x] = Mathf.FloorToInt(value * f); //random.NextInt(0,255);
                        }
                    }

                    terrainData.SetDetailLayer(0, 0, i, detailLayer);
                }
                catch (Exception) { ReportHub.LogError(reportData, $"Failed to set detail layer for {detailAsset.name}"); }
            }
        }

        private async UniTask SetHeights(int offsetX, int offsetZ, TerrainData terrainData, uint baseSeed)
        {
            int resolution = terrainGenData.chunkSize + 1;
            var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);

            var terrainHeightNoise = noiseGenCache.GetGeneratorFor(terrainGenData.terrainHeightNoise, baseSeed);
            var handle = terrainHeightNoise.Schedule(resolution, offsetX, offsetZ);
            await handle.ToUniTask(PlayerLoopTiming.Update);

            var modifyJob = new ModifyTerrainHeightJob(
                ref heights,
                in emptyParcelResult,
                in terrainHeightNoise.GetResult(),
                terrainGenData.terrainHoleEdgeSize,
                terrainGenData.minHeight,
                terrainGenData.pondDepth,
                resolution,
                offsetX,
                offsetZ,
                maxHeightIndex);

            JobHandle jobHandle = modifyJob.Schedule(heights.Length, 64);
            await jobHandle.ToUniTask(PlayerLoopTiming.Update);

            float[,] heightArray = ConvertTo2DArray(heights, resolution, resolution);
            terrainData.SetHeights(0, 0, heightArray);
            heights.Dispose();
        }

        private async UniTask SetTextures(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed)
        {
            List<INoiseGenerator> noiseGenerators = new List<INoiseGenerator>();

            foreach (NoiseData noiseData in terrainGenData.layerNoise)
            {
                if (noiseData == null) continue;

                INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(noiseData, baseSeed);
                JobHandle handle = noiseGenerator.Schedule(chunkSize, offsetX, offsetZ);

                await handle.ToUniTask(PlayerLoopTiming.Update);
                noiseGenerators.Add(noiseGenerator);
            }

            float[,,] result3D = GenerateAlphaMaps(noiseGenerators.Select(ng => ng.GetResult()).ToArray(), chunkSize, chunkSize);
            terrainData.SetAlphamaps(0, 0, result3D);
        }

        private async UniTask SetTrees(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed)
        {
            var treeInstances = new NativeHashMap<int2, TreeInstance>(5000, Allocator.Persistent);

            for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
            {
                LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                NoiseDataBase treeNoiseData = treeAsset.noiseData;

                INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                JobHandle generatorHandle = generator.Schedule(chunkSize, offsetX, offsetZ);
                await generatorHandle.ToUniTask(PlayerLoopTiming.Update);

                NativeArray<float> resultCopy = generator.GetResult();

                var treeInstancesJob = new GenerateTreeInstancesJob(
                    in resultCopy,
                    ref treeInstances,
                    in emptyParcelResult,
                    in treeAsset.randomization,
                    treeAsset.radius,
                    treeAssetIndex,
                    offsetX,
                    offsetZ,
                    chunkSize,
                    chunkSize,
                    ref random);

                JobHandle instancingHandle = treeInstancesJob.Schedule();
                await instancingHandle.ToUniTask(PlayerLoopTiming.Update);
            }

            // We do a horrible array copy because that's what the terrain API expects, it is what it is
            var array = new TreeInstance[treeInstances.Count];
            var index = 0;

            foreach (KVPair<int2, TreeInstance> treeInstance in treeInstances)
            {
                array[index] = treeInstance.Value;
                index++;
            }

            terrainData.SetTreeInstances(array, true);
            treeInstances.Dispose();
        }

        private TreePrototype[] GetTreePrototypes()
        {
            if (treePrototypes != null)
                return treePrototypes;

            treePrototypes = terrainGenData.treeAssets.Select(t => new TreePrototype
                                            {
                                                prefab = t.asset,
                                            })
                                           .ToArray();

            return treePrototypes;
        }

        // Convert flat NativeArray to a 2D array (is there another way?)
        private float[,] ConvertTo2DArray(NativeArray<float> array, int width, int height)
        {
            var result = new float[width, height];

            for (var i = 0; i < array.Length; i++)
            {
                int x = i % width;
                int z = i / width;
                result[z, x] = array[i];
            }

            return result;
        }

        /// <summary>
        /// Here we convert the result of the noise generation of the terrain texture layers
        /// </summary>
        private float[,,] GenerateAlphaMaps(NativeArray<float>[] textureResults, int width, int height)
        {
            var result = new float[width, height, terrainGenData.terrainLayers.Length];
            var valueHolder = new float[textureResults.Length];

            // every layer has the same direction, so we use the first
            int length = textureResults[0].Length;

            for (var i = 0; i < length; i++)
            {
                int x = i % width;
                int z = i / width;

                float summary = 0;

                // Get the texture value for each layer at this spot
                for (var j = 0; j < textureResults.Length; j++)
                {
                    float f = textureResults[j][i];
                    valueHolder[j] = f;
                    summary += f;
                }

                // base value is always the unfilled spot where other layers didn't draw texture
                float baseValue = Mathf.Max(0, 1 - summary);
                summary += baseValue;

                // we set the base value manually since its not part of the textureResults list
                result[z, x, 0] = baseValue / summary;

                // set the rest of the values
                for (var j = 0; j < textureResults.Length; j++)
                    result[z, x, j + 1] = textureResults[j][i] / summary;
            }

            return result;
        }

        // This should free up all the NativeArrays used for random generation, this wont affect the already generated terrain
        public void FreeMemory()
        {
            emptyParcelResult.Dispose();
            noiseGenCache.Dispose();
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroy(rootGo);
        }
    }

    public interface ITerrainGenerator { }
}
