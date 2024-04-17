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
        private readonly TerrainGeneratorLocalCache localCache;
        private readonly TimeProfiler timeProfiler;
        private readonly TerrainGenerationData terrainGenData;
        private readonly NoiseGeneratorCache noiseGenCache;

        private readonly ReportData reportData;

        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;
        private int worldSeed;

        public TerrainChunkDataGenerator(TerrainGeneratorLocalCache localCache, TimeProfiler timeProfiler, TerrainGenerationData terrainGenData, ReportData reportData, NoiseGeneratorCache noiseGenCache)
        {
            this.localCache = localCache;
            this.timeProfiler = timeProfiler;
            this.terrainGenData = terrainGenData;
            this.noiseGenCache = noiseGenCache;

            this.reportData = reportData;
        }

        public void Initialize(int worldSeed, ref NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData)
        {
            this.worldSeed = worldSeed;
            this.emptyParcelsNeighborData = emptyParcelsNeighborData;
        }

        public async UniTask SetTexturesAsync(bool useCache, int offsetX, int offsetZ, int chunkSize, TerrainData terrainData,
            uint baseSeed,
            CancellationToken cancellationToken)
        {
            if (useCache && localCache.IsValid())
            {
                timeProfiler.StartMeasure();
                float[,,] alpha = localCache.GetAlphaMaps(offsetX, offsetZ);
                terrainData.SetAlphamaps(0, 0, alpha);
                timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"- [Cache] SetTexturesAsync {t}ms"));
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

                timeProfiler.StartMeasure();
                await UniTask.WhenAll(noiseTasks).AttachExternalCancellation(cancellationToken);
                timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [AlphaMaps] Parallel Jobs {t}ms"));

                timeProfiler.StartMeasure();

                float[,,] result3D = noiseGenerators.Select(ng => ng.GetResult(noiseDataPointer))
                                                    .ToArray()
                                                    .GenerateAlphaMaps(chunkSize, chunkSize, terrainGenData.terrainLayers.Length);

                terrainData.SetAlphamaps(0, 0, result3D);

                if (useCache)
                    localCache.SaveAlphaMaps(offsetX, offsetZ, result3D);

                timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [AlphaMaps] SetAlphamaps {t}ms"));
            }
        }


        public async UniTask SetTreesAsync(int2 minTerrainParcel, int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed, CancellationToken cancellationToken, bool useCache = true)
        {
            if (useCache && localCache.IsValid())
            {
                timeProfiler.StartMeasure();
                TreeInstance[] array = localCache.GetTrees(offsetX, offsetZ);
                terrainData.SetTreeInstances(array, true);
                timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"- [Cache] SetTreesAsync {t}ms"));
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
                    timeProfiler.StartMeasure();

                    for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
                    {
                        LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                        NoiseDataBase treeNoiseData = treeAsset.noiseData;

                        treeRadiusMap.Add(treeAssetIndex, treeAsset.radius);

                        INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                        var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);
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
                            offsetX,
                            offsetZ,
                            chunkSize,
                            chunkSize,
                            minTerrainParcel,
                            treeParallelRandoms);

                        instancingHandle = treeInstancesJob.Schedule(resultReference.Length, 32, randomizerHandle);

                        generatorHandle.Complete();
                        randomizerHandle.Complete();
                        instancingHandle.Complete();
                    }

                    await instancingHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    instancingHandle.Complete();
                    timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [Trees] Parallel instancing {t}ms for {terrainGenData.treeAssets.Length} assets."));

                    timeProfiler.StartMeasure();
                    var invalidationJob = new InvalidateTreesJob(treeInstances.AsReadOnly(), treeInvalidationMap.AsParallelWriter(), treeRadiusMap.AsReadOnly(), chunkSize);
                    await invalidationJob.Schedule(chunkSize * chunkSize, 8).ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [Trees] Invalidation Job {t}ms"));

                    timeProfiler.StartMeasure();
                    var array = new List<TreeInstance>();

                    foreach (KeyValue<int2, TreeInstance> treeInstance in treeInstances)
                    {
                        // if its marked as invalid, do not use this tree
                        if (!treeInvalidationMap.TryGetValue(treeInstance.Key, out bool isInvalid)) continue;
                        if (isInvalid) continue;

                        array.Add(treeInstance.Value);
                    }

                    timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [Trees] Array Creation {t}ms"));

                    timeProfiler.StartMeasure();
                    TreeInstance[] instances = array.ToArray();
                    terrainData.SetTreeInstances(instances, true);

                    if (useCache)
                        localCache.SaveTreeInstances(offsetX, offsetZ, instances);

                    timeProfiler.EndMeasure(t => ReportHub.Log(reportData, $"    - [Trees] SetTreeInstances {t}ms"));
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
    }
}
