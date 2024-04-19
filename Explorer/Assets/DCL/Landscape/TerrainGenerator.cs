using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using StylizedGrass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using JobHandle = Unity.Jobs.JobHandle;

namespace DCL.Landscape
{
    public class TerrainGenerator : IDisposable
    {
        private const string TERRAIN_OBJECT_NAME = "Generated Terrain";

        // increment this number if we want to force the users to generate a new terrain cache
        private const int CACHE_VERSION = 1;

        private const float PROGRESS_COUNTER_EMPTY_PARCEL_DATA = 0.1f;
        private const float PROGRESS_COUNTER_TERRAIN_DATA = 0.6f;
        private const float PROGRESS_COUNTER_DIG_HOLES = 0.75f;
        private const float PROGRESS_SPAWN_TERRAIN = 0.25f;

        private readonly int parcelSize;

        private readonly TerrainGenerationData terrainGenData;
        private readonly NoiseGeneratorCache noiseGenCache;
        private readonly ReportData reportData;
        private readonly TimeProfiler timeProfiler;
        private readonly TerrainGeneratorLocalCache localCache;
        private readonly bool forceCacheRegen;
        private readonly List<Terrain> terrains;

        private readonly TerrainFactory factory;
        private readonly TerrainChunkDataGenerator chunkDataGenerator;
        private readonly TerrainBoundariesGenerator boundariesGenerator;
        private NativeArray<int2> emptyParcels;

        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;
        private NativeParallelHashMap<int2, int> emptyParcelsData;
        private NativeParallelHashSet<int2> ownedParcels;
        private int maxHeightIndex;
        private bool hideTrees;
        private bool hideDetails;
        private bool withHoles;
        private int processedTerrainDataCount;
        private int spawnedTerrainDataCount;
        private float terrainDataCount;
        private bool isTerrainGenerated;
        private bool showTerrainByDefault;

        private GameObject rootGo;

        public Transform Ocean { get; private set; }
        public List<Transform> Cliffs { get; } = new ();
        public Transform Wind { get; private set; }

        public TerrainGenerator(TerrainGenerationData terrainGenData, ref NativeArray<int2> emptyParcels, ref NativeParallelHashSet<int2> ownedParcels, bool measureTime = false, bool forceCacheRegen = false)
        {
            parcelSize = terrainGenData.parcelSize;

            this.forceCacheRegen = forceCacheRegen;
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
            noiseGenCache = new NoiseGeneratorCache();
            reportData = ReportCategory.LANDSCAPE;
            timeProfiler = new TimeProfiler(measureTime);
            localCache = new TerrainGeneratorLocalCache(terrainGenData.seed, this.terrainGenData.chunkSize, CACHE_VERSION);
            terrains = new List<Terrain>();

            factory = new TerrainFactory(terrainGenData);
            chunkDataGenerator = new TerrainChunkDataGenerator(localCache, timeProfiler, terrainGenData, reportData, noiseGenCache);
            boundariesGenerator = new TerrainBoundariesGenerator();
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroy(rootGo);
        }

        public IReadOnlyList<Terrain> GetTerrains() =>
            terrains;

        public bool IsTerrainGenerated() =>
            isTerrainGenerated;

        public void SwitchVisibility(bool isVisible)
        {
            rootGo.SetActive(isVisible);
        }

        public async UniTask GenerateTerrainAsync(
            uint worldSeed = 1,
            bool withHoles = true,
            bool hideTrees = false,
            bool hideDetails = false,
            bool showTerrainByDefault = false,
            AsyncLoadProcessReport processReport = null,
            CancellationToken cancellationToken = default)
        {
            this.showTerrainByDefault = showTerrainByDefault;

            this.hideDetails = hideDetails;
            this.hideTrees = hideTrees;
            this.withHoles = withHoles;

            var worldModel = new WorldModel(ownedParcels);
            var terrainModel = new TerrainModel(parcelSize, worldModel, terrainGenData.borderPadding);

            try
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"Terrain generation was done in {t / 1000f:F2} seconds")))
                {
                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Misc & Cliffs, Border Colliders")))
                    {
                        rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
                        Ocean = factory.CreateOcean(rootGo.transform);
                        Wind = factory.CreateWind();

                        boundariesGenerator.SpawnCliffs(terrainModel.MinInUnits, terrainModel.MaxInUnits, factory, rootGo, parcelSize);
                        boundariesGenerator.SpawnBorderColliders(terrainModel.MinInUnits, terrainModel.MaxInUnits, terrainModel.SizeInUnits, factory, rootGo, parcelSize);
                    }

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Load Local Cache")))
                        await localCache.LoadAsync(forceCacheRegen);

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Empty Parcel Setup")))
                    {
                        if (emptyParcels.Length == 0)
                            ExtractEmptyParcels(terrainModel);

                        await SetupEmptyParcelDataAsync(terrainModel, cancellationToken);
                    }

                    if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_EMPTY_PARCEL_DATA;

                    terrainDataCount = Mathf.Pow(Mathf.CeilToInt(terrainGenData.terrainSize / (float)terrainGenData.chunkSize), 2);
                    processedTerrainDataCount = 0;

                    /////////////////////////
                    // GenerateTerrainDataAsync is Sequential on purpose [ Looks nicer at the loading screen ]
                    // Each TerrainData generation uses 100% of the CPU anyway so it makes no difference running it in parallel
                    /////////////////////////
                    chunkDataGenerator.Prepare((int)worldSeed, parcelSize, ref emptyParcelsData, ref emptyParcelsNeighborData);
                    foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
                    {
                        await GenerateTerrainData(chunkModel, terrainModel, worldSeed, cancellationToken, processReport);
                        await UniTask.Yield(cancellationToken);
                    }

                    if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_DIG_HOLES;

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Chunks")))
                        await SpawnTerrainObjectsAsync(terrainModel, processReport, cancellationToken);

                    // we wait at least one frame so all the terrain chunks are properly rendered so we can render the color map
                    await UniTask.Yield();

                    AddColorMapRenderer(rootGo);

                    // waiting a frame to create the color map renderer created a new bug where some stones do not render properly, this should fix it
                    await BugWorkaroundAsync();

                    if (processReport != null) processReport.ProgressCounter.Value = 1f;
                }
            }
            catch (OperationCanceledException)
            {
                if (rootGo != null)
                    UnityObjectUtils.SafeDestroy(rootGo);
            }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, reportData); }
            finally
            {
                FreeMemory();

                if (!localCache.IsValid())
                    localCache.Save();

                isTerrainGenerated = true;
            }
        }

        private void ExtractEmptyParcels(TerrainModel terrainModel)
        {
            var tempEmptyParcels = new List<int2>();

            for (int x = terrainModel.MinParcel.x; x <= terrainModel.MaxParcel.x; x++)
            for (int y = terrainModel.MinParcel.y; y <= terrainModel.MaxParcel.y; y++)
            {
                var currentParcel = new int2(x, y);

                if (!ownedParcels.Contains(currentParcel))
                    tempEmptyParcels.Add(currentParcel);
            }

            emptyParcels = new NativeArray<int2>(tempEmptyParcels.Count, Allocator.Persistent);

            for (var i = 0; i < tempEmptyParcels.Count; i++)
                emptyParcels[i] = tempEmptyParcels[i];
        }

        private static void DigHoles(TerrainModel terrainModel, ChunkModel chunkModel, int parcelSize)
        {
            var holes = new bool[terrainModel.ChunkSizeInUnits, terrainModel.ChunkSizeInUnits];

            for (var x = 0; x < terrainModel.ChunkSizeInUnits; x++)
            for (var y = 0; y < terrainModel.ChunkSizeInUnits; y++)
                holes[x, y] = true;

            if (chunkModel.OccupiedParcels.Count > 0)
                foreach (int2 parcel in chunkModel.OccupiedParcels)
                {
                    int x = (parcel.x - chunkModel.MinParcel.x) * parcelSize;
                    int y = (parcel.y - chunkModel.MinParcel.y) * parcelSize;

                    for (int i = x; i < x + parcelSize; i++)
                    for (int j = y; j < y + parcelSize; j++)
                        holes[j, i] = false;
                }

            if (chunkModel.OutOfTerrainParcels.Count > 0)
                foreach (int2 parcel in chunkModel.OutOfTerrainParcels)
                {
                    int x = (parcel.x - chunkModel.MinParcel.x) * parcelSize;
                    int y = (parcel.y - chunkModel.MinParcel.y) * parcelSize;

                    for (int i = x; i < x + parcelSize; i++)
                    for (int j = y; j < y + parcelSize; j++)
                        holes[j, i] = false;
                }

            chunkModel.TerrainData.SetHoles(0, 0, holes);
        }

        private async Task BugWorkaroundAsync()
        {
            foreach (Terrain terrain in terrains)
                terrain.enabled = false;

            await UniTask.Yield();

            foreach (Terrain terrain in terrains)
                terrain.enabled = true;
        }

        private async UniTask SetupEmptyParcelDataAsync(TerrainModel terrainModel, CancellationToken cancellationToken)
        {
            if (localCache.IsValid())
                maxHeightIndex = localCache.GetMaxHeight();
            else
            {
                JobHandle handle = TerrainGenerationUtils.SetupEmptyParcelsJobs(
                    ref emptyParcelsData, ref emptyParcelsNeighborData,
                    in emptyParcels, ref ownedParcels,
                    terrainModel.MinParcel, terrainModel.MaxParcel,
                    terrainGenData.heightScaleNerf);

                await handle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);

                // Calculate this outside the jobs since they are Parallel
                foreach (KeyValue<int2, int> emptyParcelHeight in emptyParcelsData)
                    if (emptyParcelHeight.Value > maxHeightIndex)
                        maxHeightIndex = emptyParcelHeight.Value;

                localCache.SetMaxHeight(maxHeightIndex);
            }
        }

        private async UniTask SpawnTerrainObjectsAsync(TerrainModel terrainModel, AsyncLoadProcessReport processReport, CancellationToken cancellationToken)
        {
            foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                terrains.Add(
                    factory.CreateTerrainObject(chunkModel.TerrainData, rootGo.transform, chunkModel.MinParcel * parcelSize, terrainGenData.terrainMaterial, showTerrainByDefault));

                await UniTask.Yield();
                spawnedTerrainDataCount++;
                if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_DIG_HOLES + (spawnedTerrainDataCount / terrainDataCount * PROGRESS_SPAWN_TERRAIN);
            }
        }

        private async UniTask GenerateTerrainData(ChunkModel chunkModel, TerrainModel terrainModel, uint worldSeed, CancellationToken cancellationToken, AsyncLoadProcessReport processReport)
        {
            timeProfiler.StartMeasure();
            cancellationToken.ThrowIfCancellationRequested();

            chunkModel.TerrainData = factory.CreateTerrainData(terrainModel.ChunkSizeInUnits, maxHeightIndex);

            var tasks = new List<UniTask>
            {
                chunkDataGenerator.SetHeightsAsync(chunkModel.MinParcel, maxHeightIndex, parcelSize, chunkModel.TerrainData, worldSeed, cancellationToken, false),
                chunkDataGenerator.SetTexturesAsync(chunkModel.MinParcel.x * parcelSize, chunkModel.MinParcel.y * parcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken, false),
                !hideDetails ? chunkDataGenerator.SetDetailsAsync(chunkModel.MinParcel.x * parcelSize, chunkModel.MinParcel.y * parcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken, true, chunkModel.MinParcel, chunkModel.OccupiedParcels, false): UniTask.CompletedTask,
                !hideTrees ?chunkDataGenerator.SetTreesAsync(chunkModel.MinParcel, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken, false): UniTask.CompletedTask,
            };

            if (withHoles)
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Holes")))
                    DigHoles(terrainModel, chunkModel, parcelSize);
                // await DigHolesAsync(terrainDataDictionary, cancellationToken);
            }

            await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);

            processedTerrainDataCount++;
            if (processReport != null) processReport.ProgressCounter.Value = PROGRESS_COUNTER_EMPTY_PARCEL_DATA + (processedTerrainDataCount / terrainDataCount * PROGRESS_COUNTER_TERRAIN_DATA);
            timeProfiler.EndMeasure(t => ReportHub.Log(LogType.Log, reportData, $"[{t}ms] Terrain Data ({processedTerrainDataCount}/{terrainDataCount})"));
        }

        /// <summary>
        ///     This method digs holes on the terrain based on the ownedParcels array
        /// </summary>
        /// <param name="terrainDatas"></param>
        /// <param name="cancellationToken"></param>
        private async UniTask DigHolesAsync(Dictionary<int2, TerrainData> terrainDatas, CancellationToken cancellationToken)
        {
            if (localCache.IsValid())
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"- [Cache] DigHoles from Cache {t}ms")))
                {
                    foreach ((int2 key, TerrainData value) in terrainDatas)
                    {
                        bool[,] holes = localCache.GetHoles(key.x, key.y);
                        value.SetHoles(0, 0, holes);
                        await UniTask.Yield(cancellationToken);
                    }
                }
            }
            else
            {
                int resolution = terrainGenData.chunkSize;
                Dictionary<int2, NativeList<int2>> ownedParcelsByChunk = new ();
                var nativeHoles = new Dictionary<int2, NativeArray<bool>>();
                var originalHoles = new Dictionary<int2, bool[,]>();
                List<GCHandle> usedHandles = new ();

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"   - [DigHoles] Allocation {t}ms")))
                {
                    // initialize the holes native arrays
                    foreach (KeyValuePair<int2, TerrainData> valuePair in terrainDatas)
                    {
                        unsafe
                        {
                            var holes = new bool[resolution, resolution];

                            var holeHandle = GCHandle.Alloc(holes, GCHandleType.Pinned);
                            NativeArray<bool> nativeHole = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<bool>((void*)holeHandle.AddrOfPinnedObject(), resolution * resolution, Allocator.Persistent);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeHole, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif

                            nativeHoles.Add(valuePair.Key, nativeHole);
                            originalHoles.Add(valuePair.Key, holes);
                            usedHandles.Add(holeHandle);
                        }
                    }
                }

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"   - [DigHoles] Setup {t}ms")))
                { // get the local coordinate of each parcel and setup the data for the parallel work
                    // TODO: Can we move this into a job?
                    foreach (int2 ownedParcel in ownedParcels)
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

                        var terrainDataKey = new int2(chunkX * resolution, chunkY * resolution);
                        var holeCoordinate = new int2(localX, localY);

                        if (terrainDatas.ContainsKey(terrainDataKey))
                        {
                            if (!ownedParcelsByChunk.ContainsKey(terrainDataKey))
                                ownedParcelsByChunk.Add(terrainDataKey, new NativeList<int2>(resolution / 16 * resolution / 16, Allocator.Persistent));

                            ownedParcelsByChunk[terrainDataKey].Add(holeCoordinate);
                        }
                    }
                }

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"   - [DigHoles] Parallel Jobs {t}ms")))
                {
                    var tasks = new List<UniTask>();

                    // Schedule Parallel jobs in Parallel :)
                    foreach (KeyValuePair<int2, TerrainData> valuePair in terrainDatas)
                    {
                        NativeArray<int2> chunkOwnedParcels = ownedParcelsByChunk[valuePair.Key].AsArray();

                        var setupJob = new SetupTerrainHolesDataJob(nativeHoles[valuePair.Key]);
                        JobHandle setupJobHandle = setupJob.Schedule(resolution * resolution, 32);

                        var job = new PrepareTerrainHolesDataJob(nativeHoles[valuePair.Key], chunkOwnedParcels.AsReadOnly(), resolution);
                        JobHandle jobHandle = job.Schedule(chunkOwnedParcels.Length, 32, setupJobHandle);
                        UniTask task = jobHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                        tasks.Add(task);
                    }

                    await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);
                }

                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"   - [DigHoles] SetHoles {t}ms")))
                    foreach (KeyValuePair<int2, bool[,]> valuePair in originalHoles)
                    {
                        terrainDatas[valuePair.Key].SetHoles(0, 0, valuePair.Value);
                        localCache.SaveHoles(valuePair.Key.x, valuePair.Key.y, valuePair.Value);
                    }

                foreach ((int2 _, NativeList<int2> value) in ownedParcelsByChunk)
                    value.Dispose();

                foreach (GCHandle usedHandle in usedHandles)
                    usedHandle.Free();
            }
        }

        private void AddColorMapRenderer(GameObject parent)
        {
            (GrassColorMapRenderer colorMapRenderer, GrassColorMap grassColorMap) = factory.CreateColorMapRenderer(parent);

            colorMapRenderer.terrainObjects.AddRange(terrains.Select(t => t.gameObject));
            colorMapRenderer.RecalculateBounds();

            grassColorMap.bounds.center = new Vector3(grassColorMap.bounds.center.x, 0, grassColorMap.bounds.center.z);

            colorMapRenderer.Render();
        }

        // This should free up all the NativeArrays used for random generation, this wont affect the already generated terrain
        private void FreeMemory()
        {
            if (!localCache.IsValid())
            {
                emptyParcelsNeighborData.Dispose();
                emptyParcelsData.Dispose();
            }

            noiseGenCache.Dispose();
        }
    }
}
