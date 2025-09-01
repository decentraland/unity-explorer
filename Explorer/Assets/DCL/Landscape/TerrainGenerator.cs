using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Config;
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
using DCL.Utilities;
using Decentraland.Terrain;
using GPUInstancerPro;
using System.IO;
using System.Linq;
using System.Text;
using TerrainProto;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using Utility;
using static Unity.Mathematics.math;
using JobHandle = Unity.Jobs.JobHandle;
using TerrainData = UnityEngine.TerrainData;
using TreePrototype = UnityEngine.TreePrototype;

namespace DCL.Landscape
{
    public class TerrainGenerator : IDisposable, IContainParcel
    {
        private const string TERRAIN_OBJECT_NAME = "Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.1f; // fix for not clipping with scene (potential) floor

        // increment this number if we want to force the users to generate a new terrain cache
        private const int CACHE_VERSION = 15;

        private const float PROGRESS_COUNTER_EMPTY_PARCEL_DATA = 0.1f;
        private const float PROGRESS_COUNTER_TERRAIN_DATA = 0.3f;
        private const float PROGRESS_COUNTER_DIG_HOLES = 0.5f;
        private const float PROGRESS_SPAWN_TERRAIN = 0.25f;
        private const float PROGRESS_SPAWN_RE_ENABLE_TERRAIN = 0.25f;
        private readonly NoiseGeneratorCache noiseGenCache;
        private readonly ReportData reportData;
        private readonly TimeProfiler timeProfiler;
        private readonly IMemoryProfiler profilingProvider;
        public bool forceCacheRegen;
        private readonly List<Terrain> terrains;
        private readonly List<Collider> terrainChunkColliders;

        private TerrainGenerationData terrainGenData;
        private TerrainGeneratorLocalCache localCache;
        private TerrainChunkDataGenerator chunkDataGenerator;
        private TerrainBoundariesGenerator boundariesGenerator;
        private TerrainFactory factory;

        private NativeList<int2> emptyParcels;
        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;
        private NativeParallelHashMap<int2, int> emptyParcelsData;
        private NativeParallelHashSet<int2> ownedParcels;
        public int MaxHeight { get; private set; }
        private bool hideTrees;
        private bool hideDetails;
        private bool withHoles;
        private int processedTerrainDataCount;
        private int spawnedTerrainDataCount;
        private float terrainDataCount;

        public Transform TerrainRoot { get; private set; }
        private GrassColorMapRenderer grassRenderer;
        private bool isInitialized;
        private int activeChunk = -1;

        public int ParcelSize { get; private set; }
        public Transform Ocean { get; private set; }
        public Transform Wind { get; private set; }
        public IReadOnlyList<Transform> Cliffs { get; private set; }

        public IReadOnlyList<Terrain> Terrains => terrains;

        public bool IsTerrainGenerated { get; private set; }
        public bool IsTerrainShown { get; private set; }

        public TerrainModel TerrainModel { get; private set; }
        public Texture2D OccupancyMap { get; private set; }
        public int OccupancyFloor { get; private set; }

        private ITerrainDetailSetter terrainDetailSetter;
        private IGPUIWrapper gpuiWrapper;
        private NativeArray<byte> occupancyMapData;
        private int occupancyMapSize;
        private float terrainHeight;
        private Decentraland.Terrain.TerrainData newTerrainData;
        private int2 treeMinParcel;
        private int2 treeMaxParcel;
        private NativeArray<int> treeIndices;
        private NativeArray<TreeInstanceData> treeInstances;

        private const int TERRAIN_SIZE_LIMIT = 512; // 512x512 parcels
        private const int TREE_INSTANCE_LIMIT = 262144; // 2^18 trees

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
            terrainChunkColliders = new List<Collider>();
        }

        // TODO : pre-calculate once and re-use
        public void SetTerrainCollider(Vector2Int parcel, bool isEnabled)
        {
            if (TerrainModel == null) return;

            int offsetX = parcel.x - TerrainModel.MinParcel.x;
            int offsetY = parcel.y - TerrainModel.MinParcel.y;

            int chunkX = offsetX / TerrainModel.ChunkSizeInParcels;
            int chunkY = offsetY / TerrainModel.ChunkSizeInParcels;

            int chunkIndex = chunkX + (chunkY * TerrainModel.SizeInChunks);

            if (chunkIndex < 0 || chunkIndex >= terrainChunkColliders.Count)
                return;

            if (chunkIndex != activeChunk && activeChunk >= 0)
                terrainChunkColliders[activeChunk].enabled = false;

            terrainChunkColliders[chunkIndex].enabled = isEnabled;
            activeChunk = chunkIndex;
        }

        public void Initialize(Decentraland.Terrain.TerrainData newTerrainData, TerrainGenerationData terrainGenData, ref NativeList<int2> emptyParcels,
            ref NativeParallelHashSet<int2> ownedParcels, string parcelChecksum, bool isZone, IGPUIWrapper gpuiWrapper, ITerrainDetailSetter terrainDetailSetter,
            float terrainHeight)
        {
            this.newTerrainData = newTerrainData;
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
            this.terrainHeight = terrainHeight;

            ParcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
            localCache = new TerrainGeneratorLocalCache(terrainGenData.seed, this.terrainGenData.chunkSize,
                CACHE_VERSION, parcelChecksum, isZone);

            chunkDataGenerator = new TerrainChunkDataGenerator(localCache, timeProfiler, terrainGenData, reportData);
            boundariesGenerator = new TerrainBoundariesGenerator(factory, ParcelSize);

            this.terrainDetailSetter = terrainDetailSetter;
            this.gpuiWrapper = gpuiWrapper;
            gpuiWrapper.SetupLocalCache(localCache);

            isInitialized = true;
        }

        public bool Contains(Vector2Int parcel)
        {
            if (IsTerrainGenerated)
                return TerrainModel.IsInsideBounds(parcel);

            return true;
        }

        public void Dispose()
        {
            if (!isInitialized) return;

            if (TerrainRoot != null)
                UnityObjectUtils.SafeDestroy(TerrainRoot);
        }

        public int GetChunkSize() =>
            terrainGenData.chunkSize;

        public async UniTask ShowAsync(AsyncLoadProcessReport postRealmLoadReport)
        {
            if (!isInitialized) return;

            if (TerrainRoot != null)
                TerrainRoot.gameObject.SetActive(true);

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

            if (TerrainRoot != null && TerrainRoot.gameObject.activeSelf)
            {
                TerrainRoot.gameObject.SetActive(false);

                foreach (var collider in terrainChunkColliders)
                    if (collider.enabled) collider.enabled = false;

                IsTerrainShown = false;
            }
        }

        public async UniTask GenerateGenesisTerrainAndShowAsync(
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
            TerrainModel = new TerrainModel(ParcelSize, worldModel, terrainGenData.borderPadding);

            float startMemory = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);

            try
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"Terrain generation was done in {t / 1000f:F2} seconds")))
                {
                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Misc & Cliffs, Border Colliders")))
                    {
                        TerrainRoot = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
                        TerrainRoot.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

                        Ocean = factory.CreateOcean(TerrainRoot);
                        Wind = factory.CreateWind();

                        Cliffs = boundariesGenerator.SpawnCliffs(TerrainModel.MinInUnits, TerrainModel.MaxInUnits);
                        boundariesGenerator.SpawnBorderColliders(TerrainModel.MinInUnits, TerrainModel.MaxInUnits, TerrainModel.SizeInUnits);
                    }

                    if (LandscapeData.LOAD_TREES_FROM_STREAMINGASSETS)
                    {
                        UniTask loadTrees = LoadTreesAsync();

                        OccupancyMap = CreateOccupancyMap(ownedParcels, TerrainModel.MinParcel, TerrainModel.MaxParcel, TerrainModel.PaddingInParcels);
                        OccupancyFloor = WriteInteriorChamferOnWhite(OccupancyMap, TerrainModel.MinParcel, TerrainModel.MaxParcel, TerrainModel.PaddingInParcels);

                        // OccupancyMap.filterMode = FilterMode.Point; // DEBUG use for clear step-like pyramid terrain base height
                        OccupancyMap.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                        occupancyMapData = OccupancyMap.GetRawTextureData<byte>();
                        occupancyMapSize = OccupancyMap.width; // width == height

                        newTerrainData.OccupancyMap = OccupancyMap;
                        newTerrainData.OccupancyFloor = OccupancyFloor;

                        Ocean = factory.CreateOcean(TerrainRoot);
                        Wind = factory.CreateWind();

                        Cliffs = boundariesGenerator.SpawnCliffs(TerrainModel.MinInUnits, TerrainModel.MaxInUnits);
                        boundariesGenerator.SpawnBorderColliders(TerrainModel.MinInUnits, TerrainModel.MaxInUnits, TerrainModel.SizeInUnits);

                        await loadTrees;
                        InstantiateTrees();
                    }

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Load Local Cache")))
                        await localCache.LoadAsync(forceCacheRegen);

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Empty Parcel Setup")))
                    {
                        TerrainGenerationUtils.ExtractEmptyParcels(TerrainModel, ref emptyParcels, ref ownedParcels);
                        await SetupEmptyParcelDataAsync(TerrainModel, cancellationToken);
                    }

                    processReport?.SetProgress(PROGRESS_COUNTER_EMPTY_PARCEL_DATA);

                    terrainDataCount = Mathf.Pow(Mathf.CeilToInt(terrainGenData.terrainSize / (float)terrainGenData.chunkSize), 2);
                    processedTerrainDataCount = 0;

                    /////////////////////////
                    // GenerateTerrainDataAsync is Sequential on purpose [ Looks nicer at the loading screen ]
                    // Each TerrainData generation uses 100% of the CPU anyway so it makes no difference running it in parallel
                    /////////////////////////
                    chunkDataGenerator.Prepare((int)worldSeed, ParcelSize, ref emptyParcelsData, ref emptyParcelsNeighborData, noiseGenCache);

                    foreach (ChunkModel chunkModel in TerrainModel.ChunkModels)
                    {
                        await GenerateTerrainDataAsync(chunkModel, TerrainModel, worldSeed, cancellationToken, processReport);
                        await UniTask.Yield(cancellationToken);
                        noiseGenCache.ResetNoiseNativeArrayProvider();
                    }

                    processReport?.SetProgress(PROGRESS_COUNTER_DIG_HOLES);

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Chunks")))
                        await SpawnTerrainObjectsAsync(TerrainModel, processReport, cancellationToken);

                    grassRenderer = await TerrainGenerationUtils.AddColorMapRendererAsync(TerrainRoot, terrains, factory);

                    await ReEnableTerrainAsync(processReport);

                    if (processReport != null) processReport.SetProgress(1f);
                }
            }
            catch (OperationCanceledException)
            {
                if (TerrainRoot != null)
                    UnityObjectUtils.SafeDestroy(TerrainRoot);
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
            ReportHub.Log(ReportCategory.LANDSCAPE, $"The landscape generation took {endMemory - startMemory}MB of memory");

            gpuiWrapper.TerrainsInstantiatedAsync(TerrainModel.ChunkModels);
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
                MaxHeight = localCache.GetMaxHeight();
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
                    if (emptyParcelHeight.Value > MaxHeight)
                        MaxHeight = emptyParcelHeight.Value;

                localCache.SetMaxHeight(MaxHeight);
            }
        }

        private async UniTask SpawnTerrainObjectsAsync(TerrainModel terrainModel, AsyncLoadProcessReport processReport, CancellationToken cancellationToken)
        {
            foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (Terrain terrain, Collider terrainCollider) = factory.CreateTerrainObject(
                    chunkModel.TerrainData, TerrainRoot.transform, chunkModel.MinParcel * ParcelSize,
                    terrainGenData.terrainMaterial);

                terrain.gameObject.SetActive(false);

                chunkModel.terrain = terrain;
                terrains.Add(terrain);
                terrainChunkColliders.Add(terrainCollider);

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

                chunkModel.TerrainData = factory.CreateTerrainData(terrainModel.ChunkSizeInUnits, MaxHeight);

                var tasks = new List<UniTask>
                {
                    chunkDataGenerator.SetHeightsAsync(chunkModel.MinParcel, MaxHeight, ParcelSize,
                        chunkModel.TerrainData, worldSeed, cancellationToken),
                    chunkDataGenerator.SetTexturesAsync(chunkModel.MinParcel.x * ParcelSize,
                        chunkModel.MinParcel.y * ParcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData,
                        worldSeed, cancellationToken),
                    !hideDetails
                        ? chunkDataGenerator.SetDetailsAsync(chunkModel.MinParcel.x * ParcelSize,
                            chunkModel.MinParcel.y * ParcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData,
                            worldSeed, cancellationToken, true, chunkModel.MinParcel, terrainDetailSetter, chunkModel.OccupiedParcels)
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
                                if (chunkModel.OutOfTerrainParcels.Count != 0)
                                {
                                    chunkModel.TerrainData.SetHoles(0, 0,
                                        await localCache.GetHolesAsync(chunkModel.MinParcel.x, chunkModel.MinParcel.y));

                                    await UniTask.Yield(cancellationToken);
                                }
                            }
                        }
                        else
                        {
                            if (chunkModel.OutOfTerrainParcels.Count != 0)
                            {
                                bool[,] holes = chunkDataGenerator.DigHoles(terrainModel, chunkModel, ParcelSize, withOwned: false);
                                chunkModel.TerrainData.SetHoles(0, 0, holes);
                                localCache.SaveHoles(chunkModel.MinParcel.x, chunkModel.MinParcel.y, holes);
                            }
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
                        var holes = await localCache.GetHolesAsync(key.x, key.y);
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

        private static Texture2D CreateOccupancyMap(NativeParallelHashSet<int2> ownedParcels, int2 minParcel,
            int2 maxParcel, int padding)
        {
            int2 terrainSize = maxParcel - minParcel + 1;
            int2 citySize = terrainSize - padding * 2;
            int textureSize = ceilpow2(cmax(terrainSize) + 2);
            int textureHalfSize = textureSize / 2;

            Texture2D occupancyMap = new Texture2D(textureSize, textureSize, TextureFormat.R8, false,
                true);

            NativeArray<byte> data = occupancyMap.GetRawTextureData<byte>();

            // 1) memset all to 0 (OCCUPIED)
            unsafe
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(data);
                UnsafeUtility.MemSet(ptr, 0, data.Length);
            }

            // 2) Fill wide horizontal WHITE stripe for FREE area rows (full width), then trim with vertical black stripes
            {
                int freeMinX = minParcel.x - padding;
                int freeMinY = minParcel.y - padding;
                int freeMaxX = maxParcel.x + padding;
                int freeMaxY = maxParcel.y + padding;

                // Clamp logical coordinates to texture logical range [-textureHalfSize .. textureHalfSize-1]
                freeMinX = Math.Max(freeMinX, -textureHalfSize);
                freeMinY = Math.Max(freeMinY, -textureHalfSize);
                freeMaxX = Math.Min(freeMaxX, textureHalfSize - 1);
                freeMaxY = Math.Min(freeMaxY, textureHalfSize - 1);

                int startRow = textureHalfSize + freeMinY;
                int endRowInclusive = textureHalfSize + freeMaxY;
                int rowCount = Math.Max(0, endRowInclusive - startRow + 1);

                // Fill entire horizontal stripe WHITE in one MemSet call
                if (rowCount > 0)
                {
                    unsafe
                    {
                        var basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(data);
                        int stripeStartByte = startRow * textureSize;
                        int stripeSizeBytes = rowCount * textureSize;
                        UnsafeUtility.MemSet(basePtr + stripeStartByte, 255, stripeSizeBytes);
                    }
                }

                // 3) Trim with vertical BLACK stripes to define actual FREE area boundaries
                if (rowCount > 0)
                {
                    int leftCol = textureHalfSize + freeMinX - 1;
                    int rightCol = textureHalfSize + freeMaxX + 1;

                    // Clamp columns to texture bounds
                    leftCol = Math.Clamp(leftCol, 0, textureSize - 1);
                    rightCol = Math.Clamp(rightCol, 0, textureSize - 1);

                    unsafe
                    {
                        var basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(data);

                        // Left vertical stripe
                        for (int row = startRow; row <= endRowInclusive; row++)
                            *(basePtr + (row * textureSize) + leftCol) = 0;

                        // Right vertical stripe
                        for (int row = startRow; row <= endRowInclusive; row++)
                            *(basePtr + (row * textureSize) + rightCol) = 0;
                    }
                }
            }

            // 4) mark owned parcels as BLACK (occupied)
            foreach (int2 owned in ownedParcels)
            {
                int tx = owned.x + textureHalfSize;
                int ty = owned.y + textureHalfSize;
                int idx = (ty * textureSize) + tx;

                if ((uint)idx < (uint)data.Length)
                    data[idx] = 0;
            }

            return occupancyMap;
        }

        private static int WriteInteriorChamferOnWhite(Texture2D r8, int2 minParcel, int2 maxParcel, int padding)
        {
            int w = r8.width, h = r8.height, n = w * h;
            NativeArray<byte> src = r8.GetRawTextureData<byte>();
            if (!src.IsCreated || src.Length != n) return 0;

            int textureHalfSize = w / 2; // Assuming square texture

            // Calculate working area bounds in texture coordinates (with padding)
            int workMinX = minParcel.x - padding;
            int workMinY = minParcel.y - padding;
            int workMaxX = maxParcel.x + padding;
            int workMaxY = maxParcel.y + padding;

            // Clamp to texture logical range [-textureHalfSize .. textureHalfSize-1]
            workMinX = Math.Max(workMinX, -textureHalfSize);
            workMinY = Math.Max(workMinY, -textureHalfSize);
            workMaxX = Math.Min(workMaxX, textureHalfSize - 1);
            workMaxY = Math.Min(workMaxY, textureHalfSize - 1);

            // Convert to texture pixel coordinates and expand by 1 to include border stripes
            int texMinX = workMinX + textureHalfSize - 1;
            int texMinY = workMinY + textureHalfSize - 1;
            int texMaxX = workMaxX + textureHalfSize + 1;
            int texMaxY = workMaxY + textureHalfSize + 1;

            // Clamp to actual texture bounds
            texMinX = Math.Max(texMinX, 0);
            texMinY = Math.Max(texMinY, 0);
            texMaxX = Math.Min(texMaxX, w - 1);
            texMaxY = Math.Min(texMaxY, h - 1);

            const int INF = 1 << 28;
            const int ORTH = 3; // 3-4 chamfer (good Euclidean approx)
            const int DIAG = 4;

            // Seed distances at BLACK pixels (occupied parcels - leave them 0), propagate into WHITE (free parcels)
            var dist = new int[n];
            bool anyBlack = false, anyWhite = false;

            // Initialize only working area pixels
            for (int y = texMinY; y <= texMaxY; y++)
            for (int x = texMinX; x <= texMaxX; x++)
            {
                int i = (y * w) + x;

                if (src[i] == 0)
                {
                    dist[i] = 0;
                    anyBlack = true;
                } // Black pixels (occupied) are seeds
                else
                {
                    dist[i] = INF;
                    anyWhite = true;
                } // White pixels (free) will get distances
            }

            if (!anyBlack || !anyWhite)
                return 0; // Nothing to do if no black or no white regions exist.

            // Forward pass - only within working area
            for (int y = texMinY; y <= texMaxY; y++)
            {
                int row = y * w;

                for (int x = texMinX; x <= texMaxX; x++)
                {
                    int i = row + x;
                    int d = dist[i];

                    if (d != 0) // skip black seeds
                    {
                        if (x > texMinX) d = Mathf.Min(d, dist[i - 1] + ORTH);
                        if (y > texMinY) d = Mathf.Min(d, dist[i - w] + ORTH);
                        if (x > texMinX && y > texMinY) d = Mathf.Min(d, dist[i - w - 1] + DIAG);
                        if (x < texMaxX && y > texMinY) d = Mathf.Min(d, dist[i - w + 1] + DIAG);
                        dist[i] = d;
                    }
                }
            }

            // Backward pass - only within working area
            for (int y = texMaxY; y >= texMinY; y--)
            {
                int row = y * w;

                for (int x = texMaxX; x >= texMinX; x--)
                {
                    int i = row + x;
                    int d = dist[i];

                    if (d != 0) // skip black seeds
                    {
                        if (x < texMaxX) d = Mathf.Min(d, dist[i + 1] + ORTH);
                        if (y < texMaxY) d = Mathf.Min(d, dist[i + w] + ORTH);
                        if (x < texMaxX && y < texMaxY) d = Mathf.Min(d, dist[i + w + 1] + DIAG);
                        if (x > texMinX && y < texMaxY) d = Mathf.Min(d, dist[i + w - 1] + DIAG);
                        dist[i] = d;
                    }
                }
            }

            // Find maximum distance and convert to pixel distance - only within working area
            var maxD = 0;

            for (int y = texMinY; y <= texMaxY; y++)
            for (int x = texMinX; x <= texMaxX; x++)
            {
                int i = (y * w) + x;
                if (src[i] != 0 && dist[i] < INF && dist[i] > maxD)
                    maxD = dist[i]; // Check white pixels
            }

            if (maxD == 0)
                return 0;

            // Convert chamfer distance to approximate pixel distance
            int maxPixelDistance = maxD / ORTH;

            // Check for overflow and warn if needed
            bool hasOverflow = maxPixelDistance > 255;

            if (hasOverflow)
            {
                ReportHub.LogError(ReportCategory.LANDSCAPE, $"Distance field overflow! Max distance {maxPixelDistance} pixels, clamping to 255");
                maxPixelDistance = 255;
            }

            // Calculate adaptive stepSize to avoid merging with black pixels
            int stepSize;

            if (maxPixelDistance <= 25)
            {
                stepSize = 10; // Default stepSize for reasonable distances
            }
            else
            {
                // Adaptive stepSize to fit large distances, ensuring minValue >= 1
                stepSize = (255 - 1) / maxPixelDistance; // 254 / maxPixelDistance
                stepSize = Mathf.Max(stepSize, 1); // Ensure at least 1
            }

            int minValue = 255 - (stepSize * maxPixelDistance);
            ReportHub.Log(ReportCategory.LANDSCAPE, $"Distance field: max chamfer={maxD}, max maxPixelDistance={maxPixelDistance}, stepSize={stepSize}, range=[{minValue}, 255]");

            // Write back: keep black at 0, map distances to [minValue, 255] range - only within working area
            for (int y = texMinY; y <= texMaxY; y++)
            {
                for (int x = texMinX; x <= texMaxX; x++)
                {
                    int i = (y * w) + x;

                    if (src[i] == 0)
                    {
                        src[i] = 0;
                        continue;
                    } // keep black pixels (occupied)

                    // Convert chamfer distance to pixel distance
                    int pixelDist = dist[i] / ORTH;
                    pixelDist = Mathf.Min(pixelDist, maxPixelDistance); // Clamp to max

                    // Map [1, maxPixelDistance] to [minValue, 255]
                    int value;

                    if (maxPixelDistance == 1)
                        value = 255; // Only one distance level, use max value
                    else
                    {
                        value = minValue + ((pixelDist - 1) * (255 - minValue) / (maxPixelDistance - 1));
                        value = Mathf.Clamp(value, minValue, 255);
                    }

                    src[i] = (byte)value;
                }
            }

            return minValue;
        }

        public bool IsParcelOccupied(int2 parcel)
        {
            if (any(parcel < TerrainModel.MinParcel) || any(parcel > TerrainModel.MaxParcel))
                return false;

            if (occupancyMapSize <= 0)
                return false;

            parcel += occupancyMapSize / 2;
            int index = (parcel.y * occupancyMapSize) + parcel.x;
            return occupancyMapData[index] == 0;
        }

        private float GetParcelBaseHeight(int2 parcel)
        {
            parcel += occupancyMapSize / 2;
            int index = (parcel.y * occupancyMapSize) + parcel.x;
            return (occupancyMapData[index] - OccupancyFloor) / (255f - OccupancyFloor) * terrainHeight;
        }

        public ReadOnlySpan<TreeInstanceData> GetTreeInstances(int2 parcel)
        {
            // If tree data has not been loaded, minParcel == maxParcel, and so this is false, and we
            // don't need to check if treeInstances is empty or anything like that.
            if (parcel.x < treeMinParcel.x || parcel.x >= treeMaxParcel.x
                                           || parcel.y < treeMinParcel.y || parcel.y >= treeMaxParcel.y)
            {
                return ReadOnlySpan<TreeInstanceData>.Empty;
            }

            int index = ((parcel.y - treeMinParcel.y) * (treeMaxParcel.x - treeMinParcel.x))
                + parcel.x - treeMinParcel.x;

            int start = treeIndices[index++];
            int end = index < treeIndices.Length ? treeIndices[index] : treeInstances.Length;
            return treeInstances.AsReadOnlySpan().Slice(start, end - start);
        }

        public bool GetTreeTransform(int2 parcel, TreeInstanceData instance, out Vector3 position,
            out Quaternion rotation, out Vector3 scale)
        {
            position.x = (parcel.x + (instance.PositionX * (1f / 255f))) * ParcelSize;
            position.z = (parcel.y + (instance.PositionZ * (1f / 255f))) * ParcelSize;

            if (OverlapsOccupiedParcel(float2(position.x, position.z),
                    terrainGenData.treeAssets[instance.PrototypeIndex].radius))
            {
                position.y = 0f;
                rotation = default;
                scale = default;
                return false;
            }

            position.y = GetParcelBaseHeight(parcel) + MountainsNoise.GetHeight(position.x, position.z);
            rotation = Quaternion.Euler(0f, instance.RotationY * (360f / 255f), 0f);

            scale = terrainGenData.treeAssets[instance.PrototypeIndex]
                                  .randomization
                                  .LerpScale(float2(instance.ScaleXZ, instance.ScaleY) * (1f / 255f))
                                  .xyx;

            return true;
        }

        private bool OverlapsOccupiedParcel(float2 position, float radius)
        {
            int2 parcel = (int2)floor(position * (1f / ParcelSize));

            if (IsParcelOccupied(parcel))
                return true;

            float2 localPosition = position - (parcel * ParcelSize);

            if (localPosition.x < radius)
            {
                if (IsParcelOccupied(int2(parcel.x - 1, parcel.y)))
                    return true;

                if (localPosition.y < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x - 1, parcel.y - 1)))
                        return true;
                }
            }

            if (ParcelSize - localPosition.x < radius)
            {
                if (IsParcelOccupied(int2(parcel.x + 1, parcel.y)))
                    return true;

                if (ParcelSize - localPosition.y < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x + 1, parcel.y + 1)))
                        return true;
                }
            }

            if (localPosition.y < radius)
            {
                if (IsParcelOccupied(int2(parcel.x, parcel.y - 1)))
                    return true;

                if (ParcelSize - localPosition.x < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x + 1, parcel.y - 1)))
                        return true;
                }
            }

            if (ParcelSize - localPosition.y < radius)
            {
                if (IsParcelOccupied(int2(parcel.x, parcel.y + 1)))
                    return true;

                if (localPosition.x < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x - 1, parcel.y + 1)))
                        return true;
                }
            }

            return false;
        }

        private static string treeFilePath => $"{Application.streamingAssetsPath}/Trees.bin";

        private static void SaveTrees(ChunkModel[] chunks, TerrainGenerationData terrainData)
        {
            var writer = new TreeInstanceWriter(terrainData.parcelSize, terrainData.treeAssets);

            foreach (ChunkModel chunk in chunks)
                writer.AddTerrain(chunk.terrain);

            using var stream = new FileStream(treeFilePath, FileMode.Create, FileAccess.Write);
            writer.Write(stream);
        }

        private async UniTask LoadTreesAsync()
        {
            try
            {
                await using var stream = new FileStream(treeFilePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read);

                using var reader = new BinaryReader(stream, new UTF8Encoding(false), false);

                treeMinParcel.x = reader.ReadInt32();
                treeMinParcel.y = reader.ReadInt32();
                treeMaxParcel.x = reader.ReadInt32();
                treeMaxParcel.y = reader.ReadInt32();

                int2 treeIndexSize = treeMaxParcel - treeMinParcel;

                if (any(treeIndexSize > TERRAIN_SIZE_LIMIT))
                    throw new IOException(
                        $"Tree index size of ({treeIndexSize.x}, {treeIndexSize.y}) exceeds the limit of {TERRAIN_SIZE_LIMIT}");

                treeIndices = new NativeArray<int>(treeIndexSize.x * treeIndexSize.y,
                    Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                ReadReliably(reader, treeIndices);

                int treeInstanceCount = reader.ReadInt32();

                if (treeInstanceCount > TREE_INSTANCE_LIMIT)
                    throw new IOException(
                        $"Tree instance count of {treeInstanceCount} exceeds the limit of {TREE_INSTANCE_LIMIT}");

                treeInstances = new NativeArray<TreeInstanceData>(treeInstanceCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

                ReadReliably(reader, treeInstances);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                    ReportHub.LogWarning(ReportCategory.LANDSCAPE,
                        $"Tree instance data file not found, path: {treeFilePath}");
                else
                    ReportHub.LogException(ex, ReportCategory.LANDSCAPE);

                if (treeIndices.IsCreated)
                    treeIndices.Dispose();

                if (treeInstances.IsCreated)
                    treeInstances.Dispose();

                treeIndices = new NativeArray<int>(0, Allocator.Persistent);
                treeInstances = new NativeArray<TreeInstanceData>(0, Allocator.Persistent);
            }
        }

        private static unsafe void ReadReliably<T>(BinaryReader reader, NativeArray<T> array)
            where T : unmanaged
        {
            var buffer = new Span<byte>(array.GetUnsafePtr(), array.Length * sizeof(T));

            while (buffer.Length > 0)
            {
                int read = reader.Read(buffer);

                if (read <= 0)
                    throw new EndOfStreamException("Read zero bytes");

                buffer = buffer.Slice(read);
            }
        }

#if GPUI_PRO_PRESENT
        private void InstantiateTrees()
        {
            LandscapeAsset[] prototypes = terrainGenData.treeAssets;
            int stride = treeMaxParcel.x - treeMinParcel.x;
            var transforms = new List<Matrix4x4>[prototypes.Length];

            for (int i = 0; i < transforms.Length; i++)
                transforms[i] = new List<Matrix4x4>();

            for (int i = 0; i < treeIndices.Length; i++)
            {
                int2 parcel = int2(i % stride, i / stride);
                ReadOnlySpan<TreeInstanceData> instances = GetTreeInstances(parcel);

                foreach (var instance in instances)
                {
                    if (GetTreeTransform(parcel, instance,
                            out Vector3 position, out Quaternion rotation, out Vector3 scale))
                    {
                        transforms[instance.PrototypeIndex]
                           .Add(Matrix4x4.TRS(position, rotation, scale));
                    }
                }
            }

            int[] rendererKeys = new int[terrainGenData.treeAssets.Length];

            for (int prototypeIndex = 0; prototypeIndex < terrainGenData.treeAssets.Length;
                 prototypeIndex++)
            {
                GPUICoreAPI.RegisterRenderer(TerrainRoot, terrainGenData.treeAssets[prototypeIndex].asset,
                    out rendererKeys[prototypeIndex]);

                GPUIRenderingSystem.SetTransformBufferData(rendererKeys[prototypeIndex],
                    transforms[prototypeIndex], 0, 0, transforms[prototypeIndex].Count);
            }
        }
#endif
    }
}
