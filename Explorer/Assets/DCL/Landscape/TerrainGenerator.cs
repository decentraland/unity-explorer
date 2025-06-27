using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;
using System.Collections.Generic;
using DCL.Profiling;
using DCL.Utilities;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using TerrainData = Decentraland.Terrain.TerrainData;

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

        private int parcelSize;
        private TerrainGenerationData terrainGenData;
        private TerrainData terrainData;
        private TerrainBoundariesGenerator boundariesGenerator;
        private TerrainFactory factory;

        private int2[] roads;
        private int2[] occupied;
        private int2[] empty;

        private Transform rootGo;
        private bool isInitialized;

        public Transform Ocean { get; private set; }
        public Transform Wind { get; private set; }
        public IReadOnlyList<Transform> Cliffs { get; private set; }

        public IReadOnlyList<Terrain> Terrains => Array.Empty<Terrain>();

        public bool IsTerrainGenerated { get; private set; }
        public bool IsTerrainShown { get; private set; }

        private TerrainModel terrainModel;

        public TerrainGenerator(IMemoryProfiler profilingProvider, bool measureTime = false)
        {
            this.profilingProvider = profilingProvider;

            reportData = ReportCategory.LANDSCAPE;
            timeProfiler = new TimeProfiler(measureTime);
        }

        public void Initialize(TerrainGenerationData terrainGenData, TerrainData terrainData,
            int2[] roads, int2[] occupied, int2[] empty)
        {
            this.terrainGenData = terrainGenData;
            this.terrainData = terrainData;
            this.roads = roads;
            this.occupied = occupied;
            this.empty = empty;

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

        public void Show(AsyncLoadProcessReport postRealmLoadReport)
        {
            if (!isInitialized) return;

            if (rootGo != null)
                rootGo.gameObject.SetActive(true);

            terrainModel.UpdateTerrainData(terrainData);
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

        public void GenerateAndShow(AsyncLoadProcessReport processReport = null)
        {
            if (!isInitialized) return;

            terrainModel = new TerrainModel(roads, occupied, empty, parcelSize,
                terrainGenData.borderPadding);

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
                    processReport?.SetProgress(PROGRESS_COUNTER_DIG_HOLES);

                    terrainModel.UpdateTerrainData(terrainData);

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

                float afterCleaning = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
                ReportHub.Log(ReportCategory.LANDSCAPE,
                    $"The landscape cleaning process cleaned {afterCleaning - beforeCleaning}MB of memory");
            }

            float endMemory = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
            ReportHub.Log(ReportCategory.LANDSCAPE, $"The landscape generation took {endMemory - startMemory}MB of memory");
        }
    }
}
