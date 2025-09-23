using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape
{
    [Obsolete]
    public class TerrainChunkDataGenerator
    {
        private readonly TimeProfiler timeProfiler;
        private readonly TerrainGenerationData terrainGenData;
        private NoiseGeneratorCache noiseGenCache;

        private readonly ReportData reportData;

        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;

        private int worldSeed;
        private int parcelSize;
        private int resolution;

        public TerrainChunkDataGenerator(object localCache, TimeProfiler timeProfiler,
            TerrainGenerationData terrainGenData, ReportData reportData)
        {
            this.timeProfiler = timeProfiler;
            this.terrainGenData = terrainGenData;

            this.reportData = reportData;
        }

        public void Prepare(int worldSeed, int parcelSize, ref NativeParallelHashMap<int2, int> emptyParcelsData, ref NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData, NoiseGeneratorCache noiseGeneratorCache)
        {
            this.worldSeed = worldSeed;
            this.emptyParcelsNeighborData = emptyParcelsNeighborData;
            this.parcelSize = parcelSize;

            this.noiseGenCache = noiseGeneratorCache;
        }

        public async UniTask SetTreesAsync(int2 chunkMinParcel, int chunkSize, List<TreeInstance> terrainData, uint baseSeed, CancellationToken cancellationToken,
            bool useCache = true)
        {
            if (useCache)
            {
                // This code path has been deleted.
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();

                var treeInstances = new NativeParallelHashMap<int2, TreeInstance>(chunkSize * chunkSize, Allocator.Persistent);
                var treeInvalidationMap = new NativeParallelHashMap<int2, bool>(chunkSize * chunkSize, Allocator.Persistent);
                var treeRadiusMap = new NativeHashMap<int, TreeRadiusPair>(terrainGenData.treeAssets.Length, Allocator.Persistent);
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

                            var treeRadiusPair = new TreeRadiusPair { radius = treeAsset.radius, secondaryRadius = treeAsset.secondaryRadius };
                            treeRadiusMap.Add(treeAssetIndex, treeRadiusPair);

                            INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                            var noiseDataPointer = new NoiseDataPointer(chunkSize, chunkMinParcel.x, chunkMinParcel.y);
                            JobHandle generatorHandle = generator.Schedule(noiseDataPointer, instancingHandle);

                            var randomizer = new SetupRandomForParallelJobs(treeParallelRandoms, worldSeed);
                            JobHandle randomizerHandle = randomizer.Schedule(generatorHandle);

                            NativeArray<float> resultReference = generator.GetResult(noiseDataPointer);

                            var treeInstancesJob = new GenerateTreeInstancesJob(
                                resultReference.AsReadOnly(),
                                treeInstances.AsParallelWriter(),
                                emptyParcelsNeighborData.AsReadOnly(),
                                in treeAsset.randomization,
                                treeRadiusPair,
                                treeAssetIndex,
                                chunkMinParcel,
                                chunkSize,
                                parcelSize,
                                treeParallelRandoms,
                                false);

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
                        terrainData.AddRange(instances);
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
    }
}
