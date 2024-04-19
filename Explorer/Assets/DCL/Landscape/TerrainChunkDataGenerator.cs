using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape
{
    public class TerrainChunkDataGenerator
    {
        private const int UNITY_MAX_COVERAGE_VALUE = 255;
        private const int UNITY_MAX_INSTANCE_COUNT = 16;

        private readonly TerrainGeneratorLocalCache localCache;
        private readonly TimeProfiler timeProfiler;
        private readonly TerrainGenerationData terrainGenData;
        private readonly NoiseGeneratorCache noiseGenCache;

        private readonly ReportData reportData;

        private NativeParallelHashMap<int2, int> emptyParcelsData;
        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;

        private int worldSeed;
        private int parcelSize;
        private int resolution;

        public TerrainChunkDataGenerator(TerrainGeneratorLocalCache localCache, TimeProfiler timeProfiler, TerrainGenerationData terrainGenData, ReportData reportData, NoiseGeneratorCache noiseGenCache)
        {
            this.localCache = localCache;
            this.timeProfiler = timeProfiler;
            this.terrainGenData = terrainGenData;
            this.noiseGenCache = noiseGenCache;

            this.reportData = reportData;
        }

        public void Prepare(int worldSeed, int parcelSize, ref NativeParallelHashMap<int2, int> emptyParcelsData, ref NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData)
        {
            this.worldSeed = worldSeed;
            this.emptyParcelsNeighborData = emptyParcelsNeighborData;
            this.emptyParcelsData = emptyParcelsData;
            this.parcelSize = parcelSize;
        }

        public async UniTask SetHeightsAsync(int2 chunkMinParcel, int maxHeightIndex, int parcelSize,
            TerrainData terrainData, uint baseSeed, CancellationToken cancellationToken, bool useCache = true)
        {
            int parcelMinX = chunkMinParcel.x * parcelSize;
            int parcelMinZ = chunkMinParcel.y * parcelSize;

            if (useCache && localCache.IsValid())
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"- [Cache] SetHeightsAsync from Cache {t}ms")))
                {
                    float[,] heightArray = localCache.GetHeights(parcelMinX, parcelMinZ);
                    terrainData.SetHeights(0, 0, heightArray);
                }
            }
            else
            {
                timeProfiler.StartMeasure();
                resolution = terrainData.heightmapResolution;
                var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);

                INoiseGenerator terrainHeightNoise = noiseGenCache.GetGeneratorFor(terrainGenData.terrainHeightNoise, baseSeed);
                var noiseDataPointer = new NoiseDataPointer(resolution, parcelMinX, parcelMinZ);
                JobHandle handle = terrainHeightNoise.Schedule(noiseDataPointer, default(JobHandle));

                NativeArray<float> terrainNoise = terrainHeightNoise.GetResult(noiseDataPointer);

                var modifyJob = new ModifyTerrainHeightJob(
                    ref heights,
                    in emptyParcelsNeighborData, in emptyParcelsData,
                    in terrainNoise,
                    terrainGenData.terrainHoleEdgeSize,
                    terrainGenData.minHeight,
                    terrainGenData.pondDepth,
                    resolution,
                    chunkMinParcel,
                    maxHeightIndex,
                    parcelSize
                );

                JobHandle jobHandle = modifyJob.Schedule(heights.Length, 64, handle);

                try
                {
                    await jobHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [SetHeightsAsync] Noise Job + ModifyTerrainHeightJob Job {t}ms"));

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [SetHeightsAsync] SetHeights {t}ms")))
                    {
                        float[,] heightArray = heights.To2DArray(resolution, resolution);
                        terrainData.SetHeights(0, 0, heightArray);

                        if (useCache)
                            localCache.SaveHeights(parcelMinX, parcelMinZ, heightArray);
                    }
                }
                finally { heights.Dispose(); }
            }
        }

        public async UniTask SetTexturesAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken,
            bool useCache = true)
        {
            if (useCache && localCache.IsValid())
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"- [Cache] SetTexturesAsync {t}ms")))
                {
                    float[,,] alpha = localCache.GetAlphaMaps(offsetX, offsetZ);
                    terrainData.SetAlphamaps(0, 0, alpha);
                }
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                var noiseGenerators = new List<INoiseGenerator>();
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

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [AlphaMaps] Parallel Jobs {t}ms")))
                    await UniTask.WhenAll(noiseTasks).AttachExternalCancellation(cancellationToken);

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [AlphaMaps] SetAlphamaps {t}ms")))
                {
                    float[,,] result3D = noiseGenerators.Select(ng => ng.GetResult(noiseDataPointer))
                                                        .ToArray()
                                                        .GenerateAlphaMaps(chunkSize, chunkSize, terrainGenData.terrainLayers.Length);

                    terrainData.SetAlphamaps(0, 0, result3D);

                    if (useCache)
                        localCache.SaveAlphaMaps(offsetX, offsetZ, result3D);
                }
            }
        }

        public async UniTask SetTreesAsync(int2 chunkMinParcel, int chunkSize, TerrainData terrainData,
            uint baseSeed, CancellationToken cancellationToken, bool useCache = true)
        {
            if (useCache && localCache.IsValid())
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"- [Cache] SetTreesAsync from Cache {t}ms")))
                {
                    TreeInstance[] array = localCache.GetTrees(chunkMinParcel.x, chunkMinParcel.y);
                    terrainData.SetTreeInstances(array, true);
                }
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();

                var treeInstances = new NativeParallelHashMap<int2, TreeInstance>(chunkSize * chunkSize, Allocator.Persistent);
                var treeInvalidationMap = new NativeParallelHashMap<int2, bool>(chunkSize * chunkSize, Allocator.Persistent);
                var treeRadiusMap = new NativeHashMap<int, float>(terrainGenData.treeAssets.Length, Allocator.Persistent);
                var treeParallelRandoms = new NativeArray<Random>(chunkSize * chunkSize, Allocator.Persistent);

                JobHandle instancingHandle = default;

                try
                {
                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [Trees] Parallel instancing {t}ms for {terrainGenData.treeAssets.Length} assets.")))
                    {
                        for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
                        {
                            LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                            NoiseDataBase treeNoiseData = treeAsset.noiseData;

                            treeRadiusMap.Add(treeAssetIndex, treeAsset.radius);

                            INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                            var noiseDataPointer = new NoiseDataPointer(chunkSize, chunkMinParcel.x,chunkMinParcel.y);
                            JobHandle generatorHandle = generator.Schedule(noiseDataPointer, instancingHandle);

                            var randomizer = new SetupRandomForParallelJobs(treeParallelRandoms, worldSeed);
                            JobHandle randomizerHandle = randomizer.Schedule(generatorHandle);

                            NativeArray<float> resultReference = generator.GetResult(noiseDataPointer);

                            var treeInstancesJob = new GenerateTreeInstancesJob(
                                resultReference.AsReadOnly(),
                                treeInstances.AsParallelWriter(),
                                emptyParcelsNeighborData.AsReadOnly(),
                                in treeAsset.randomization,
                                treeAsset.radius,
                                treeAssetIndex,
                                chunkMinParcel,
                                chunkSize,
                                parcelSize,
                                treeParallelRandoms,
                                false
                            );

                            instancingHandle = treeInstancesJob.Schedule(resultReference.Length, 32, randomizerHandle);

                            generatorHandle.Complete();
                            randomizerHandle.Complete();
                            instancingHandle.Complete();
                        }

                        await instancingHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                        instancingHandle.Complete();
                    }

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [Trees] Invalidation Job {t}ms")))
                    {
                        var invalidationJob = new InvalidateTreesJob(treeInstances.AsReadOnly(), treeInvalidationMap.AsParallelWriter(), treeRadiusMap.AsReadOnly(), chunkSize);
                        await invalidationJob.Schedule(chunkSize * chunkSize, 8).ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    }

                    var array = new List<TreeInstance>();

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [Trees] Array Creation {t}ms")))
                    {
                        foreach (KeyValue<int2, TreeInstance> treeInstance in treeInstances)
                        {
                            // if its marked as invalid, do not use this tree
                            if (!treeInvalidationMap.TryGetValue(treeInstance.Key, out bool isInvalid)) continue;

                            if (isInvalid) continue;

                            array.Add(treeInstance.Value);
                        }
                    }

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [Trees] SetTreeInstances {t}ms")))
                    {
                        TreeInstance[] instances = array.ToArray();
                        terrainData.SetTreeInstances(instances, true);

                        if (useCache)
                            localCache.SaveTreeInstances(chunkMinParcel.x, chunkMinParcel.y, instances);
                    }
                }
                catch (Exception e) { ReportHub.LogException(e, reportData); }
                finally
                {
                    treeInstances.Dispose();
                    treeInvalidationMap.Dispose();
                    treeRadiusMap.Dispose();
                    treeParallelRandoms.Dispose();
                }
            }
        }

        public async UniTask SetDetailsAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken, bool nullifyDetailsOnOwned, int2 chunkMinParcel, List<int2> chunkOccupiedParcels = null, bool useCache = true)
        {
            terrainData.SetDetailScatterMode(terrainGenData.detailScatterMode);

            if (useCache && localCache.IsValid())
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"- [Cache] SetDetails from Cache {t}ms")))
                    for (var i = 0; i < terrainGenData.detailAssets.Length; i++)
                    {
                        int[,] detailLayer = localCache.GetDetailLayer(offsetX, offsetZ, i);
                        terrainData.SetDetailLayer(0, 0, i, detailLayer);
                    }
            }
            else
            {
                var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);
                var generators = new List<INoiseGenerator>();

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [SetDetailsAsync] Wait for Parallel Jobs {t}ms")))
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
                }

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"    - [SetDetailsAsync] SetDetailLayer in Parallel {t}ms")))
                {
                    for (var i = 0; i < terrainGenData.detailAssets.Length; i++)
                    {
                        NativeArray<float> result = generators[i].GetResult(noiseDataPointer);

                        int[,] detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, i);

                        for (var y = 0; y < chunkSize; y++)
                        for (var x = 0; x < chunkSize; x++)
                        {
                            int f = terrainGenData.detailScatterMode == DetailScatterMode.CoverageMode ? UNITY_MAX_COVERAGE_VALUE : UNITY_MAX_INSTANCE_COUNT;
                            float value = result[x + (y * chunkSize)];

                            detailLayer[y, x] = Mathf.FloorToInt(value * f);
                        }

                        // no details for owned parcels (in case when we have a terrain on owned parcels instead of holes, like in Worlds)
                        if (nullifyDetailsOnOwned && chunkOccupiedParcels != null)
                            foreach (int2 parcel in chunkOccupiedParcels)
                                for (int y = (-chunkMinParcel.y + parcel.y) * parcelSize; y < (-chunkMinParcel.y + parcel.y + 1) * parcelSize; y++)
                                for (int x = (-chunkMinParcel.x + parcel.x) * parcelSize; x < (-chunkMinParcel.x + parcel.x + 1) * parcelSize; x++)
                                    detailLayer[y, x] = 0;

                        terrainData.SetDetailLayer(0, 0, i, detailLayer);

                        if (useCache)
                            localCache.SaveDetailLayer(offsetX, offsetZ, i, detailLayer);
                    }
                }
            }
        }
    }
}
