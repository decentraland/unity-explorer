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
using System.Runtime.InteropServices;
using System.Threading;
using DCL.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;
using JobHandle = Unity.Jobs.JobHandle;

namespace DCL.Landscape
{
    public class TerrainGenerator : IDisposable
    {
        private const string TERRAIN_OBJECT_NAME = "Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.01f; // fix for not clipping with scene (potential) floor

        // increment this number if we want to force the users to generate a new terrain cache
        private const int CACHE_VERSION = 7;

        private const float PROGRESS_COUNTER_EMPTY_PARCEL_DATA = 0.1f;
        private const float PROGRESS_COUNTER_TERRAIN_DATA = 0.3f;
        private const float PROGRESS_COUNTER_DIG_HOLES = 0.5f;
        private const float PROGRESS_SPAWN_TERRAIN = 0.25f;
        private const float PROGRESS_SPAWN_RE_ENABLE_TERRAIN = 0.25f;
        private readonly NoiseGeneratorCache noiseGenCache;
        private readonly ReportData reportData;
        private readonly TimeProfiler timeProfiler;
        private readonly IMemoryProfiler profilingProvider;
        private readonly bool forceCacheRegen;
        private readonly List<Terrain> terrains;

        private int parcelSize;
        private TerrainGenerationData terrainGenData;
        private TerrainGeneratorLocalCache localCache;
        private TerrainChunkDataGenerator chunkDataGenerator;
        private TerrainBoundariesGenerator boundariesGenerator;
        private TerrainFactory factory;

        private NativeList<int2> emptyParcels;
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

        private Transform rootGo;
        private GrassColorMapRenderer grassRenderer;
        private bool isInitialized;

        public Transform Ocean { get; private set; }
        public Transform Wind { get; private set; }
        public IReadOnlyList<Transform> Cliffs { get; private set; }

        public IReadOnlyList<Terrain> Terrains => terrains;

        public bool IsTerrainGenerated { get; private set; }
        public bool IsTerrainShown { get; private set; }


        public TerrainGenerator(IMemoryProfiler profilingProvider, bool measureTime = false,
            bool forceCacheRegen = false)
        {
            this.profilingProvider = profilingProvider;
            this.forceCacheRegen = forceCacheRegen;

            noiseGenCache = new NoiseGeneratorCache();
            reportData = ReportCategory.LANDSCAPE;
            timeProfiler = new TimeProfiler(measureTime);

            // TODO (Vit): we can make it an array and init after constructing the TerrainModel, because we will know the size
            terrains = new List<Terrain>();
        }

        public void Initialize(TerrainGenerationData terrainGenData, ref NativeList<int2> emptyParcels, ref NativeParallelHashSet<int2> ownedParcels, string parcelChecksum)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;

            parcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
            localCache = new TerrainGeneratorLocalCache(terrainGenData.seed, this.terrainGenData.chunkSize, CACHE_VERSION, parcelChecksum);

            chunkDataGenerator = new TerrainChunkDataGenerator(localCache, timeProfiler, terrainGenData, reportData);
            boundariesGenerator = new TerrainBoundariesGenerator(factory, parcelSize);

            isInitialized = true;
        }

        public void Dispose()
        {
            if (!isInitialized) return;

            if (rootGo != null)
                UnityObjectUtils.SafeDestroy(rootGo);
        }

        public int GetChunkSize() =>
            terrainGenData.chunkSize;

        public async UniTask ShowAsync(AsyncLoadProcessReport postRealmLoadReport)
        {
            if (!isInitialized) return;

            if (rootGo != null)
                rootGo.gameObject.SetActive(true);

            UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            ReEnableChunksDetails();

            if(grassRenderer)
                grassRenderer.Render();

            await ReEnableTerrainAsync(postRealmLoadReport);
            IsTerrainShown = true;

            postRealmLoadReport.SetProgress(1f);
        }

        public void Hide()
        {
            if (!isInitialized) return;

            if (rootGo != null && rootGo.gameObject.activeSelf)
            {
                rootGo.gameObject.SetActive(false);
                IsTerrainShown = false;
            }
        }

        public async UniTask GenerateTerrainAndShowAsync(
            uint worldSeed = 1,
            bool withHoles = true,
            bool hideTrees = false,
            bool hideDetails = false,
            AsyncLoadProcessReport processReport = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized) return;

            this.hideDetails = hideDetails;
            this.hideTrees = hideTrees;
            this.withHoles = withHoles;

            var worldModel = new WorldModel(ownedParcels);
            var terrainModel = new TerrainModel(parcelSize, worldModel, terrainGenData.borderPadding);

            float startMemory = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
            
            try
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"Terrain generation was done in {t / 1000f:F2} seconds")))
                {
                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Misc & Cliffs, Border Colliders")))
                    {
                        rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
                        rootGo.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

                        Ocean = factory.CreateOcean(rootGo);
                        Wind = factory.CreateWind();

                        Cliffs = boundariesGenerator.SpawnCliffs(terrainModel.MinInUnits, terrainModel.MaxInUnits);
                        boundariesGenerator.SpawnBorderColliders(terrainModel.MinInUnits, terrainModel.MaxInUnits, terrainModel.SizeInUnits);
                    }

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Load Local Cache")))
                        await localCache.LoadAsync(forceCacheRegen);

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Empty Parcel Setup")))
                    {
                        TerrainGenerationUtils.ExtractEmptyParcels(terrainModel, ref emptyParcels, ref ownedParcels);
                        await SetupEmptyParcelDataAsync(terrainModel, cancellationToken);
                    }

                    if (processReport != null) processReport.SetProgress(PROGRESS_COUNTER_EMPTY_PARCEL_DATA);

                    terrainDataCount = Mathf.Pow(Mathf.CeilToInt(terrainGenData.terrainSize / (float)terrainGenData.chunkSize), 2);
                    processedTerrainDataCount = 0;

                    /////////////////////////
                    // GenerateTerrainDataAsync is Sequential on purpose [ Looks nicer at the loading screen ]
                    // Each TerrainData generation uses 100% of the CPU anyway so it makes no difference running it in parallel
                    /////////////////////////
                    chunkDataGenerator.Prepare((int)worldSeed, parcelSize, ref emptyParcelsData, ref emptyParcelsNeighborData, noiseGenCache);

                    foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
                    {
                        await GenerateTerrainDataAsync(chunkModel, terrainModel, worldSeed, cancellationToken, processReport);
                        await UniTask.Yield(cancellationToken);
                    }

                    if (processReport != null) processReport.SetProgress(PROGRESS_COUNTER_DIG_HOLES);

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Chunks")))
                        await SpawnTerrainObjectsAsync(terrainModel, processReport, cancellationToken);

                    grassRenderer = await TerrainGenerationUtils.AddColorMapRendererAsync(rootGo, terrains, factory);

                    await ReEnableTerrainAsync(processReport);

                    if (processReport != null) processReport.SetProgress(1f);
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
                float beforeCleaning = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
                FreeMemory();

                if (!localCache.IsValid())
                    localCache.Save();

                IsTerrainGenerated = true;
                IsTerrainShown = true;

                emptyParcels.Dispose();
                ownedParcels.Dispose();

                float afterCleaning = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
                ReportHub.Log(ReportCategory.LANDSCAPE,
                    $"The landscape cleaning process cleaned {afterCleaning - beforeCleaning}MB of memory");
            }

            float endMemory = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
            ReportHub.Log(ReportCategory.LANDSCAPE,
                $"The landscape generation took {endMemory - startMemory}MB of memory");
        }

        // waiting a frame to create the color map renderer created a new bug where some stones do not render properly, this should fix it
        private async UniTask ReEnableTerrainAsync(AsyncLoadProcessReport processReport, int batch = 1)
        {
            foreach (Terrain terrain in terrains)
                terrain.enabled = false;

            // we enable them one by batches to avoid a super hiccup
            var i = 0;
            while (i < terrains.Count)
            {
                await UniTask.Yield();

                // Process batch
                for (int j = i; j < Math.Min(i + batch, terrains.Count); j++)
                {
                    terrains[j].enabled = true;
                    if (processReport != null) processReport.SetProgress(PROGRESS_COUNTER_DIG_HOLES + PROGRESS_SPAWN_TERRAIN + j / terrainDataCount * PROGRESS_SPAWN_RE_ENABLE_TERRAIN);
                }

                i += batch;
                if (i >= terrains.Count) break;
            }
        }

        private void ReEnableChunksDetails()
        {
            foreach (Terrain terrain in terrains)
            {
                terrain.drawHeightmap = true;
                terrain.drawTreesAndFoliage = true;
            }
        }

        private async UniTask SetupEmptyParcelDataAsync(TerrainModel terrainModel, CancellationToken cancellationToken)
        {
            if (localCache.IsValid())
                maxHeightIndex = localCache.GetMaxHeight();
            else
            {
                JobHandle handle = TerrainGenerationUtils.SetupEmptyParcelsJobs(
                    ref emptyParcelsData, ref emptyParcelsNeighborData,
                    emptyParcels.AsArray(), ref ownedParcels,
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
                    factory.CreateTerrainObject(chunkModel.TerrainData, rootGo.transform, chunkModel.MinParcel * parcelSize, terrainGenData.terrainMaterial));

                await UniTask.Yield();
                spawnedTerrainDataCount++;
                if (processReport != null) processReport.SetProgress(PROGRESS_COUNTER_DIG_HOLES + spawnedTerrainDataCount / terrainDataCount * PROGRESS_SPAWN_TERRAIN);
            }
        }

        private async UniTask GenerateTerrainDataAsync(ChunkModel chunkModel, TerrainModel terrainModel, uint worldSeed, CancellationToken cancellationToken, AsyncLoadProcessReport processReport)
        {
            using (timeProfiler.Measure(t => ReportHub.Log(LogType.Log, reportData, $"[{t}ms] Terrain Data ({processedTerrainDataCount}/{terrainDataCount})")))
            {
                cancellationToken.ThrowIfCancellationRequested();

                chunkModel.TerrainData = factory.CreateTerrainData(terrainModel.ChunkSizeInUnits, maxHeightIndex);

                var tasks = new List<UniTask>
                {
                    chunkDataGenerator.SetHeightsAsync(chunkModel.MinParcel, maxHeightIndex, parcelSize,
                        chunkModel.TerrainData, worldSeed, cancellationToken),
                    chunkDataGenerator.SetTexturesAsync(chunkModel.MinParcel.x * parcelSize,
                        chunkModel.MinParcel.y * parcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData,
                        worldSeed, cancellationToken),
                    !hideDetails
                        ? chunkDataGenerator.SetDetailsAsync(chunkModel.MinParcel.x * parcelSize,
                            chunkModel.MinParcel.y * parcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData,
                            worldSeed, cancellationToken, true, chunkModel.MinParcel, chunkModel.OccupiedParcels)
                        : UniTask.CompletedTask,
                    !hideTrees
                        ? chunkDataGenerator.SetTreesAsync(chunkModel.MinParcel, terrainModel.ChunkSizeInUnits,
                            chunkModel.TerrainData, worldSeed, cancellationToken)
                        : UniTask.CompletedTask
                };

                if (withHoles)
                {
                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Holes")))
                    {
                        if (localCache.IsValid())
                        {
                            using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"- [Cache] DigHoles from Cache {t}ms")))
                            {
                                chunkModel.TerrainData.SetHoles(0, 0,
                                    await localCache.GetHoles(chunkModel.MinParcel.x, chunkModel.MinParcel.y));
                                await UniTask.Yield(cancellationToken);
                            }
                        }
                        else
                        {
                            bool[,] holes = chunkDataGenerator.DigHoles(terrainModel, chunkModel, parcelSize, withOwned: false);
                            chunkModel.TerrainData.SetHoles(0, 0, holes);
                            localCache.SaveHoles(chunkModel.MinParcel.x, chunkModel.MinParcel.y, holes);
                        }

                        // await DigHolesAsync(terrainDataDictionary, cancellationToken);
                    }
                }

                await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);

                processedTerrainDataCount++;
                if (processReport != null) processReport.SetProgress(PROGRESS_COUNTER_EMPTY_PARCEL_DATA + processedTerrainDataCount / terrainDataCount * PROGRESS_COUNTER_TERRAIN_DATA);
            }
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
                        var holes = await localCache.GetHoles(key.x, key.y);
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
