using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Landscape
{
    public class WorldTerrainGenerator
    {
        private const string TERRAIN_OBJECT_NAME = "Generated Terrain";

        private readonly TerrainGenerationData terrainGenData;
        private readonly NoiseGeneratorCache noiseGenCache;

        private GameObject rootGo;

        private NativeParallelHashSet<int2> ownedParcels;
        private NativeArray<int2> emptyParcels;
        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelNeighborHeightsData;
        private NativeParallelHashMap<int2, int> emptyParcelHeights;

        private int maxHeightIndex;
        private readonly List<Terrain> terrains;

        public WorldTerrainGenerator(TerrainGenerationData terrainGenData, ref NativeParallelHashSet<int2> ownedParcels)
        {
            this.terrainGenData = terrainGenData;

            this.ownedParcels = ownedParcels;

            noiseGenCache = new NoiseGeneratorCache();
            terrains = new List<Terrain>();
        }

        public async UniTask GenerateTerrainAsync(uint worldSeed = 1, CancellationToken cancellationToken = default)
        {
            CalculateEmptyParcels(4);

            rootGo = InstantiateSingletonTerrainRoot();
            rootGo.transform.position = new Vector3(-terrainGenData.terrainSize / 2f, 0, -terrainGenData.terrainSize / 2f);

            await SetupEmptyParcelHeightDataAsync(cancellationToken);

            var terrainDatas = new Dictionary<int2, TerrainData>();

            for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
            for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
            {
                TerrainData terrainData = await GenerateTerrainDataAsync(x, z, worldSeed, cancellationToken);
                terrainDatas.Add(new int2(x, z), terrainData);
                await UniTask.Yield(cancellationToken);
            }

            await GenerateChunksAsync(terrainDatas, cancellationToken: cancellationToken);
        }

        private void CalculateEmptyParcels(int onSideSize)
        {
            var length = onSideSize * onSideSize;
            emptyParcels = new NativeArray<int2>(length, Allocator.Persistent);

            int offset = onSideSize / 2; // Offset to shift indices from 0-based to -size/2 based
            for (int index = 0; index < length; index++)
            {
                int i = (index % onSideSize) - offset;
                int j = (index / onSideSize) - offset;

                var parcel = new int2(i, j);

                if (!ownedParcels.Contains(parcel))
                {
                    emptyParcels[index] = parcel;
                    Debug.Log($"VVV empty added {parcel}");
                }
            }
        }

        private GameObject InstantiateSingletonTerrainRoot()
        {
            rootGo = GameObject.Find(TERRAIN_OBJECT_NAME);

            if (rootGo != null)
                UnityObjectUtils.SafeDestroy(rootGo);

            return new GameObject(TERRAIN_OBJECT_NAME);
        }

        private async UniTask SetupEmptyParcelHeightDataAsync(CancellationToken cancellationToken)
        {
            {
                emptyParcelHeights = new NativeParallelHashMap<int2, int>(emptyParcels.Length, Allocator.Persistent);
                emptyParcelNeighborHeightsData = new NativeParallelHashMap<int2, EmptyParcelNeighborData>(emptyParcels.Length, Allocator.Persistent);

                var job = new CalculateEmptyParcelBaseHeightJob(in emptyParcels, ownedParcels.AsReadOnly(), emptyParcelHeights.AsParallelWriter(), terrainGenData.heightScaleNerf);
                JobHandle handle = job.Schedule(emptyParcels.Length, 32);

                var job2 = new CalculateEmptyParcelNeighbourHeights(in emptyParcels, in ownedParcels, emptyParcelNeighborHeightsData.AsParallelWriter(), emptyParcelHeights.AsReadOnly());
                JobHandle handle2 = job2.Schedule(emptyParcels.Length, 32, handle);

                await handle2.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);

                // Calculate this outside the jobs since they are Parallel
                foreach (KeyValue<int2, int> emptyParcelHeight in emptyParcelHeights)
                    if (emptyParcelHeight.Value > maxHeightIndex)
                        maxHeightIndex = emptyParcelHeight.Value;

                // localCache.SetMaxHeight(maxHeightIndex);
            }
        }

        private async UniTask<TerrainData> GenerateTerrainDataAsync(int offsetX, int offsetZ, uint baseSeed, CancellationToken cancellationToken, AsyncLoadProcessReport processReport = null)
        {
            // using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t}ms] Terrain Data ({processedTerrainDataCount}/{terrainDataCount})")))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int resolution = terrainGenData.chunkSize;
                int chunkSize = terrainGenData.chunkSize;

                var terrainData = new TerrainData
                {
                    heightmapResolution = resolution + 1,
                    alphamapResolution = resolution,
                    size = new Vector3(chunkSize, maxHeightIndex, chunkSize),
                    terrainLayers = terrainGenData.terrainLayers,

                    // treePrototypes = GetTreePrototypes(),
                    // detailPrototypes = GetDetailPrototypes(),
                };

                terrainData.SetDetailResolution(chunkSize, 32);

                var tasks = new List<UniTask>();
                UniTask heights = SetHeightsAsync(offsetX, offsetZ, terrainData, baseSeed, cancellationToken);
                tasks.Add(heights);

                //
                // UniTask textures = SetTexturesAsync(offsetX, offsetZ, resolution, terrainData, baseSeed, cancellationToken);
                // tasks.Add(textures);
                //
                // if (!hideTrees)
                // {
                //     UniTask trees = SetTreesAsync(offsetX, offsetZ, chunkSize, terrainData, baseSeed, cancellationToken);
                //     tasks.Add(trees);
                // }
                //
                // if (!hideDetails)
                // {
                //     UniTask details = SetDetailsAsync(offsetX, offsetZ, chunkSize, terrainData, baseSeed, cancellationToken);
                //     tasks.Add(details);
                // }
                //
                // processedTerrainDataCount++;

                await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);

                // if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_EMPTY_PARCEL_DATA + (processedTerrainDataCount / terrainDataCount * PROGRESS_COUNTER_TERRAIN_DATA);

                return terrainData;
            }
        }

        private async UniTask SetHeightsAsync(int offsetX, int offsetZ, TerrainData terrainData, uint baseSeed, CancellationToken cancellationToken)
        {
            {
                int resolution = terrainGenData.chunkSize + 1;
                var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);

                INoiseGenerator terrainHeightNoise = noiseGenCache.GetGeneratorFor(terrainGenData.terrainHeightNoise, baseSeed);
                var noiseDataPointer = new NoiseDataPointer(resolution, offsetX, offsetZ);
                JobHandle handle = terrainHeightNoise.Schedule(noiseDataPointer, default(JobHandle));

                NativeArray<float> terrainNoise = terrainHeightNoise.GetResult(noiseDataPointer);

                var modifyJob = new ModifyTerrainHeightJob(
                    ref heights,
                    in emptyParcelNeighborHeightsData, in emptyParcelHeights,
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

                    float[,] heightArray = ConvertTo2DArray(heights, resolution, resolution);
                    terrainData.SetHeights(0, 0, heightArray);
                }
                finally { heights.Dispose(); }
            }
        }

        private async UniTask GenerateChunksAsync(Dictionary<int2, TerrainData> terrainDatas, AsyncLoadProcessReport processReport = null, CancellationToken cancellationToken = default)
        {
            for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
            for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TerrainData terrainData = terrainDatas[new int2(x, z)];
                GenerateTerrainChunk(x, z, terrainData, terrainGenData.terrainMaterial);
                await UniTask.Yield();
                // if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_DIG_HOLES + (++spawnedTerrainDataCount / terrainDataCount * PROGRESS_SPAWN_TERRAIN);
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
            terrain.drawHeightmap = true; // forced to true for the color map renderer
            terrain.drawTreesAndFoliage = true;

            terrainObject.transform.position = new Vector3(offsetX, -terrainGenData.minHeight, offsetZ);
            terrainObject.transform.SetParent(rootGo.transform, false);

            terrains.Add(terrain);
        }

        // Convert flat NativeArray to a 2D array (is there another way?)
        private static float[,] ConvertTo2DArray(NativeArray<float> array, int width, int height)
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
    }
}
