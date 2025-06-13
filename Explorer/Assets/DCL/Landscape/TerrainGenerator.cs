using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using StylizedGrass;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Profiling;
using DCL.Utilities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace DCL.Landscape
{
    public class TerrainGenerator : IDisposable, IContainParcel
    {
        private const string TERRAIN_OBJECT_NAME = "Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.1f; // fix for not clipping with scene (potential) floor

        private const float PROGRESS_COUNTER_EMPTY_PARCEL_DATA = 0.1f;
        private const float PROGRESS_COUNTER_DIG_HOLES = 0.5f;
        private readonly ReportData reportData;
        private readonly TimeProfiler timeProfiler;
        private readonly IMemoryProfiler profilingProvider;
        private readonly List<Terrain> terrains;
        private readonly List<Collider> terrainChunkColliders;

        private int parcelSize;
        private TerrainGenerationData terrainGenData;
        private TerrainBoundariesGenerator boundariesGenerator;
        private TerrainFactory factory;

        private NativeList<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;
        private int maxHeightIndex;
        private int processedTerrainDataCount;
        private int spawnedTerrainDataCount;

        private Transform rootGo;
        private GrassColorMapRenderer grassRenderer;
        private bool isInitialized;
        private int activeChunk = -1;

        public Transform Ocean { get; private set; }
        public Transform Wind { get; private set; }
        public IReadOnlyList<Transform> Cliffs { get; private set; }

        public IReadOnlyList<Terrain> Terrains => terrains;

        public bool IsTerrainGenerated { get; private set; }
        public bool IsTerrainShown { get; private set; }

        public Action<List<Terrain>> GenesisTerrainGenerated;

        private TerrainModel terrainModel;

        public TerrainGenerator(IMemoryProfiler profilingProvider, bool measureTime = false,
            bool forceCacheRegen = false)
        {
            this.profilingProvider = profilingProvider;

            reportData = ReportCategory.LANDSCAPE;
            timeProfiler = new TimeProfiler(measureTime);

            // TODO (Vit): we can make it an array and init after constructing the TerrainModel, because we will know the size
            terrains = new List<Terrain>();
            terrainChunkColliders = new List<Collider>();
        }

        // TODO : pre-calculate once and re-use
        public void SetTerrainCollider(Vector2Int parcel, bool isEnabled)
        {
            if(terrainModel == null) return;

            int offsetX = parcel.x - terrainModel.MinParcel.x;
            int offsetY = parcel.y - terrainModel.MinParcel.y;

            int chunkX = offsetX / terrainModel.ChunkSizeInParcels;
            int chunkY = offsetY / terrainModel.ChunkSizeInParcels;

            int chunkIndex = chunkX + (chunkY * terrainModel.SizeInChunks);

            if (chunkIndex < 0 || chunkIndex >= terrainChunkColliders.Count)
                return;

            if (chunkIndex != activeChunk && activeChunk >= 0)
                terrainChunkColliders[activeChunk].enabled = false;

            terrainChunkColliders[chunkIndex].enabled = isEnabled;
            activeChunk = chunkIndex;

        }

        public void Initialize(TerrainGenerationData terrainGenData, ref NativeList<int2> emptyParcels,
            ref NativeParallelHashSet<int2> ownedParcels, string parcelChecksum, bool isZone)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;

            parcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);

            boundariesGenerator = new TerrainBoundariesGenerator(factory, parcelSize);

            isInitialized = true;
        }

        public bool Contains(Vector2Int parcel)
        {
            if (IsTerrainGenerated)
                return terrainModel.IsInsideBounds(parcel);

            return true;
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

            IsTerrainShown = true;

            postRealmLoadReport.SetProgress(1f);
        }

        public void Hide()
        {
            if (!isInitialized) return;

            if (rootGo != null && rootGo.gameObject.activeSelf)
            {
                rootGo.gameObject.SetActive(false);

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

            var worldModel = new WorldModel(ownedParcels);
            terrainModel = new TerrainModel(parcelSize, worldModel, terrainGenData.borderPadding);

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

                    processReport?.SetProgress(PROGRESS_COUNTER_EMPTY_PARCEL_DATA);

                    processedTerrainDataCount = 0;

                    processReport?.SetProgress(PROGRESS_COUNTER_DIG_HOLES);

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

            GenesisTerrainGenerated?.Invoke(terrains);
        }
    }
}
