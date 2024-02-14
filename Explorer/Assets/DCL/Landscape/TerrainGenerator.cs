using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using StylizedGrass;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;
using JobHandle = Unity.Jobs.JobHandle;
using Object = UnityEngine.Object;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape
{
    public class TerrainGenerator : ITerrainGenerator, IDisposable
    {
        private const float PROGRESS_COUNTER_EMPTY_PARCEL_DATA = 0.1f;
        private const float PROGRESS_COUNTER_TERRAIN_DATA = 0.7f;
        private const float PROGRESS_COUNTER_DIG_HOLES = 0.9f;

        private const int UNITY_MAX_COVERAGE_VALUE = 255;
        private const int UNITY_MAX_INSTANCE_COUNT = 16;

        private readonly TerrainGenerationData terrainGenData;
        private GameObject rootGo;
        private TreePrototype[] treePrototypes;
        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelNeighborData;
        private NativeParallelHashMap<int2, int> emptyParcelHeights;
        private NativeArray<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;
        private int maxHeightIndex;
        private Random random;
        private readonly NoiseGeneratorCache noiseGenCache;
        private bool hideTrees;
        private bool hideDetails;
        private readonly ReportData reportData;

        private int processedTerrainDataCount;
        private float terrainDataCount;
        private readonly TimeProfiler timeProfiler;

        public TerrainGenerator(TerrainGenerationData terrainGenData, ref NativeArray<int2> emptyParcels, ref NativeParallelHashSet<int2> ownedParcels, bool measureTime = false)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
            noiseGenCache = new NoiseGeneratorCache();
            reportData = new ReportData("TERRAIN_GENERATOR");
            timeProfiler = new TimeProfiler(measureTime);
        }

        public async UniTask GenerateTerrainAsync(
            uint worldSeed = 1,
            bool withHoles = true,
            bool centerTerrain = true,
            bool hideTrees = false,
            bool hideDetails = false,
            AsyncLoadProcessReport processReport = null,
            CancellationToken cancellationToken = default)
        {
            this.hideDetails = hideDetails;
            this.hideTrees = hideTrees;

            try
            {
                timeProfiler.StartMeasure();

                random = new Random((uint)terrainGenData.seed);

                timeProfiler.StartMeasure();
                await SetupEmptyParcelDataAsync(cancellationToken);
                timeProfiler.EndMeasure(t => ReportHub.Log(LogType.Log, reportData, $"[{t:F2}ms] Empty Parcel Setup"));


                if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_EMPTY_PARCEL_DATA;

                rootGo = GameObject.Find("Generated Terrain");
                if (rootGo != null) UnityObjectUtils.SafeDestroy(rootGo);
                rootGo = new GameObject("Generated Terrain");

                timeProfiler.StartMeasure();
                SpawnMiscAsync();
                GenerateCliffs();
                timeProfiler.EndMeasure(t => ReportHub.Log(LogType.Log, reportData, $"[{t:F2}ms] Misc & Cliffs"));

                if (centerTerrain)
                    rootGo.transform.position = new Vector3(-terrainGenData.terrainSize / 2f, 0, -terrainGenData.terrainSize / 2f);

                terrainDataCount = Mathf.Pow(Mathf.CeilToInt(terrainGenData.terrainSize / (float)terrainGenData.chunkSize), 2);
                processedTerrainDataCount = 0;

                /////////////////////////
                // GenerateTerrainDataAsync is Sequential on purpose [ Looks nicer at the loading screen ]
                // Each TerrainData generation uses 100% of the CPU anyway so it makes no difference running it in parallel
                /////////////////////////

                var terrainDataDictionary = new Dictionary<int2, TerrainData>();

                for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
                for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
                {
                    KeyValuePair<int2, TerrainData> generateTerrainDataAsync = await GenerateTerrainDataAsync(x, z, worldSeed, cancellationToken, processReport);
                    terrainDataDictionary.Add(generateTerrainDataAsync.Key, generateTerrainDataAsync.Value);
                }

                if (withHoles)
                {
                    timeProfiler.StartMeasure();
                    DigHoles(terrainDataDictionary, cancellationToken);
                    timeProfiler.EndMeasure(t => ReportHub.Log(LogType.Log, reportData, $"[{t:F2}ms] Holes"));
                }

                if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_DIG_HOLES;

                timeProfiler.StartMeasure();
                GenerateChunks(terrainDataDictionary, cancellationToken);
                timeProfiler.EndMeasure(t => ReportHub.Log(LogType.Log, reportData, $"[{t:F2}ms] Chunks"));

                if (processReport != null) processReport.ProgressCounter.Value = 1f;

                timeProfiler.EndMeasure(t => ReportHub.Log(LogType.Log, reportData, $"Terrain generation was done in {t / 1000f:F2} seconds"));
            }
            catch (OperationCanceledException)
            {
                if (rootGo != null) UnityObjectUtils.SafeDestroy(rootGo);
            }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, reportData); }
            finally { FreeMemory(); }
        }

        private void SpawnMiscAsync()
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

        private async UniTask SetupEmptyParcelDataAsync(CancellationToken cancellationToken)
        {
            emptyParcelNeighborData = new NativeParallelHashMap<int2, EmptyParcelNeighborData>(emptyParcels.Length, Allocator.Persistent);
            emptyParcelHeights = new NativeParallelHashMap<int2, int>(emptyParcels.Length, Allocator.Persistent);

            var job = new CalculateEmptyParcelBaseHeightJob(in emptyParcels, ownedParcels.AsReadOnly(), emptyParcelHeights.AsParallelWriter(), terrainGenData.heightScaleNerf);
            JobHandle handle = job.Schedule(emptyParcels.Length, 32);

            var job2 = new CalculateEmptyParcelNeighbourHeights(in emptyParcels, in ownedParcels, emptyParcelNeighborData.AsParallelWriter(), emptyParcelHeights.AsReadOnly());
            JobHandle handle2 = job2.Schedule(emptyParcels.Length, 32, handle);

            await handle2.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);

            // Calculate this outside the jobs since they are Parallel
            foreach (KeyValue<int2, int> emptyParcelHeight in emptyParcelHeights)
                if (emptyParcelHeight.Value > maxHeightIndex)
                    maxHeightIndex = emptyParcelHeight.Value;
        }

        // TODO: THROTTLE ME
        private void GenerateChunks(Dictionary<int2, TerrainData> terrainDatas, CancellationToken cancellationToken)
        {
            for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
            for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TerrainData terrainData = terrainDatas[new int2(x, z)];
                GenerateTerrainChunk(x, z, terrainData, terrainGenData.terrainMaterial);
            }
        }

        /// <summary>
        ///     This method digs holes on the terrain based on the ownedParcels array
        /// </summary>
        /// <param name="terrainDatas"></param>
        /// <param name="cancellationToken"></param>

        // TODO: THROTTLE ME
        private void DigHoles(Dictionary<int2, TerrainData> terrainDatas, CancellationToken cancellationToken)
        {
            int resolution = terrainGenData.chunkSize;

            var parcelSizeHole = new bool[16, 16];

            for (var i = 0; i < 16; i++)
            for (var j = 0; j < 16; j++)
                parcelSizeHole[i, j] = false;

            foreach (int2 ownedParcel in ownedParcels)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int parcelGlobalX = (150 + ownedParcel.x) * 16;
                    int parcelGlobalY = (150 + ownedParcel.y) * 16;

                    // Calculate the terrain chunk index for the parcel
                    int chunkX = Mathf.FloorToInt((float)parcelGlobalX / resolution);
                    int chunkY = Mathf.FloorToInt((float)parcelGlobalY / resolution);

                    // Calculate the position within the terrain chunk
                    int localX = parcelGlobalX - (chunkX * resolution);
                    int localY = parcelGlobalY - (chunkY * resolution);

                    TerrainData terrainData = terrainDatas[new int2(chunkX * resolution, chunkY * resolution)];
                    terrainData.SetHoles(localX, localY, parcelSizeHole);
                }
                catch (Exception)
                {
                    // This will fail constantly when building terrains in the terrain generator test for smaller terrains, so we ignore those exceptions.
                }
            }
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

            //AddColormapRenderer(terrainObject); Disabled for now
        }

        private static void AddColormapRenderer(GameObject terrainObject)
        {
            GrassColorMapRenderer colorMapRenderer = terrainObject.AddComponent<GrassColorMapRenderer>();
            colorMapRenderer.terrainObjects.Add(terrainObject);
            colorMapRenderer.RecalculateBounds();
            colorMapRenderer.Render();
        }

        private async UniTask<KeyValuePair<int2, TerrainData>> GenerateTerrainDataAsync(int offsetX, int offsetZ, uint baseSeed, CancellationToken cancellationToken, AsyncLoadProcessReport processReport)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            var tasks = new List<UniTask>();
            UniTask heights = SetHeightsAsync(offsetX, offsetZ, terrainData, baseSeed, cancellationToken);
            tasks.Add(heights);

            UniTask textures = SetTexturesAsync(offsetX, offsetZ, resolution, terrainData, baseSeed, cancellationToken);
            tasks.Add(textures);

            if (!hideTrees)
            {
                UniTask trees = SetTreesAsync(offsetX, offsetZ, chunkSize, terrainData, baseSeed, cancellationToken);
                tasks.Add(trees);
            }

            if (!hideDetails)
            {
                UniTask details = SetDetailsAsync(offsetX, offsetZ, chunkSize, terrainData, baseSeed, cancellationToken);
                tasks.Add(details);
            }

            processedTerrainDataCount++;

            timeProfiler.StartMeasure();
            await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);

            if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_EMPTY_PARCEL_DATA + (processedTerrainDataCount / terrainDataCount * PROGRESS_COUNTER_TERRAIN_DATA);
            timeProfiler.EndMeasure(t => ReportHub.Log(LogType.Log, reportData, $"[{t}ms] Terrain Data ({processedTerrainDataCount}/{terrainDataCount})"));

            return new KeyValuePair<int2, TerrainData>(new int2(offsetX, offsetZ), terrainData);
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

        private async UniTask SetDetailsAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);

            timeProfiler.StartMeasure();

            cancellationToken.ThrowIfCancellationRequested();

            terrainData.SetDetailScatterMode(terrainGenData.detailScatterMode);

            var generators = new List<INoiseGenerator>();
            var tasks = new List<UniTask>();

            for (var i = 0; i < terrainGenData.detailAssets.Length; i++)
            {
                LandscapeAsset detailAsset = terrainGenData.detailAssets[i];

                try
                {
                    INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(detailAsset.noiseData, baseSeed);
                    JobHandle handle = noiseGenerator.Schedule(noiseDataPointer, default(JobHandle));
                    UniTask task = handle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    generators.Add(noiseGenerator);
                    tasks.Add(task);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogError(reportData, $"Failed to set detail layer for {detailAsset.name}");
                    ReportHub.LogException(e, reportData);
                }
            }

            await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);

            timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [SetDetailsAsync] Wait for Parallel Jobs {t}ms"));
            timeProfiler.StartMeasure();

            for (var i = 0; i < terrainGenData.detailAssets.Length; i++)
            {
                NativeArray<float> result = generators[i].GetResult(noiseDataPointer);

                int[,] detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, i);

                for (var y = 0; y < chunkSize; y++)
                {
                    for (var x = 0; x < chunkSize; x++)
                    {
                        int f = terrainGenData.detailScatterMode == DetailScatterMode.CoverageMode ? UNITY_MAX_COVERAGE_VALUE : UNITY_MAX_INSTANCE_COUNT;
                        int index = x + (y * chunkSize);
                        float value = result[index];
                        detailLayer[y, x] = Mathf.FloorToInt(value * f);
                    }
                }

                terrainData.SetDetailLayer(0, 0, i, detailLayer);
            }

            timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [SetDetailsAsync] SetDetailLayer in Parallel {t}ms"));
        }

        private async UniTask SetHeightsAsync(int offsetX, int offsetZ, TerrainData terrainData, uint baseSeed, CancellationToken cancellationToken)
        {
            var sw3 = new Stopwatch();
            sw3.Start();

            int resolution = terrainGenData.chunkSize + 1;
            var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);

            var terrainHeightNoise = noiseGenCache.GetGeneratorFor(terrainGenData.terrainHeightNoise, baseSeed);
            var noiseDataPointer = new NoiseDataPointer(resolution, offsetX, offsetZ);
            JobHandle handle = terrainHeightNoise.Schedule(noiseDataPointer, default(JobHandle));

            //await handle.ToUniTask(PlayerLoopTiming.Update);
            /*ReportHub.Log(reportData, $"    - [SetHeightsAsync] Noise Job {sw3.ElapsedMilliseconds}ms");
            sw3.Reset();
            sw3.Start();*/

            NativeArray<float> terrainNoise = terrainHeightNoise.GetResult(noiseDataPointer);

            var modifyJob = new ModifyTerrainHeightJob(
                ref heights,
                in emptyParcelNeighborData, in emptyParcelHeights,
                in terrainNoise,
                terrainGenData.terrainHoleEdgeSize,
                terrainGenData.minHeight,
                terrainGenData.pondDepth,
                resolution,
                offsetX,
                offsetZ,
                maxHeightIndex);

            JobHandle jobHandle = modifyJob.Schedule(heights.Length, 64, handle);

            try
            {
                await jobHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                ReportHub.Log(reportData, $"    - [SetHeightsAsync] Noise Job + ModifyTerrainHeightJob Job {sw3.ElapsedMilliseconds}ms");
                sw3.Reset();
                sw3.Start();

                float[,] heightArray = ConvertTo2DArray(heights, resolution, resolution);
                terrainData.SetHeights(0, 0, heightArray);
                ReportHub.Log(reportData, $"    - [SetHeightsAsync] SetHeights {sw3.ElapsedMilliseconds}ms");
                sw3.Reset();
                sw3.Start();
            }
            finally { heights.Dispose(); }
        }

        private async UniTask SetTexturesAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<INoiseGenerator> noiseGenerators = new List<INoiseGenerator>();
            var noiseTasks = new List<UniTask>();
            var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);

            foreach (NoiseData noiseData in terrainGenData.layerNoise)
            {
                if (noiseData == null) continue;

                INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(noiseData, baseSeed);
                JobHandle handle = noiseGenerator.Schedule(noiseDataPointer, default(JobHandle));

                UniTask noiseTask = handle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                noiseTasks.Add(noiseTask);
                noiseGenerators.Add(noiseGenerator);
            }

            timeProfiler.StartMeasure();
            await UniTask.WhenAll(noiseTasks).AttachExternalCancellation(cancellationToken);
            timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [AlphaMaps] Parallel Jobs {t}ms"));

            timeProfiler.StartMeasure();
            // TODO: remove usage of SetAlphamaps and instead use terrainData.GetAlphamapTexture() and fill the texture with a Compute Shader
            float[,,] result3D = GenerateAlphaMaps(noiseGenerators.Select(ng => ng.GetResult(noiseDataPointer)).ToArray(), chunkSize, chunkSize);
            terrainData.SetAlphamaps(0, 0, result3D);

            timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [AlphaMaps] SetAlphamaps {t}ms"));

        }

        private async UniTask SetTreesAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            var sw3 = new Stopwatch();
            sw3.Start();

            cancellationToken.ThrowIfCancellationRequested();

            var treeInstances = new NativeParallelHashMap<int2, TreeInstance>(chunkSize * chunkSize, Allocator.Persistent);
            var treeInvalidationMap = new NativeParallelHashMap<int2, bool>(chunkSize * chunkSize, Allocator.Persistent);
            var treeRadiusMap = new NativeHashMap<int, float>(terrainGenData.treeAssets.Length, Allocator.Persistent);

            try
            {
                var sw2 = new Stopwatch();
                sw2.Start();
                var instancingHandle = default(JobHandle);

                for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
                {
                    LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                    NoiseDataBase treeNoiseData = treeAsset.noiseData;

                    treeRadiusMap.Add(treeAssetIndex, treeAsset.radius);

                    INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                    var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);
                    JobHandle generatorHandle = generator.Schedule(noiseDataPointer, instancingHandle);

                    NativeArray<float> resultReference = generator.GetResult(noiseDataPointer);

                    var treeInstancesJob = new GenerateTreeInstancesJob(
                        resultReference.AsReadOnly(),
                        treeInstances.AsParallelWriter(),
                        emptyParcelNeighborData.AsReadOnly(),
                        in treeAsset.randomization,
                        treeAsset.radius,
                        treeAssetIndex,
                        offsetX,
                        offsetZ,
                        chunkSize,
                        chunkSize,
                        ref random);

                    instancingHandle = treeInstancesJob.Schedule(resultReference.Length, 32, generatorHandle);
                }

                await instancingHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                ReportHub.Log(reportData, $"    - [Trees] Parallel instancing {sw2.ElapsedMilliseconds}ms");
                sw2.Reset();
                sw2.Start();

                var invalidationJob = new InvalidateTreesJob(treeInstances.AsReadOnly(), treeInvalidationMap.AsParallelWriter(), treeRadiusMap.AsReadOnly(), chunkSize);
                await invalidationJob.Schedule(chunkSize * chunkSize, 8).ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);

                //await invalidationJob.Schedule().ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                ReportHub.Log(reportData, $"    - [Trees] Invalidation Job {sw2.ElapsedMilliseconds}ms");
                sw2.Reset();
                sw2.Start();

                var array = new List<TreeInstance>();

                foreach (KeyValue<int2, TreeInstance> treeInstance in treeInstances)
                {
                    // if its marked as invalid, do not use this tree
                    if (!treeInvalidationMap.TryGetValue(treeInstance.Key, out bool isInvalid)) continue;
                    if (isInvalid) continue;

                    array.Add(treeInstance.Value);
                }

                ReportHub.Log(reportData, $"    - [Trees] Array Creation {sw2.ElapsedMilliseconds}ms");
                sw2.Reset();
                sw2.Start();

                terrainData.SetTreeInstances(array.ToArray(), true);
                ReportHub.Log(reportData, $"    - [Trees] SetTreeInstances {sw2.ElapsedMilliseconds}ms");
                sw2.Stop();
            }
            catch (Exception e) { ReportHub.LogException(e, reportData); }
            finally
            {
                treeInstances.Dispose();
                treeInvalidationMap.Dispose();
                treeRadiusMap.Dispose();

                //ReportHub.Log(reportData, $"    SetTreesAsync {sw3.ElapsedMilliseconds/1000f:F2}s");
                sw3.Stop();
            }
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
        private void FreeMemory()
        {
            emptyParcelNeighborData.Dispose();
            noiseGenCache.Dispose();
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroy(rootGo);
        }
    }

    public interface ITerrainGenerator { }
}
