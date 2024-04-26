using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
using Utility;

namespace DCL.Landscape
{
    public class WorldTerrainGenerator : IDisposable
    {
        private const string TERRAIN_OBJECT_NAME = "World Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.001f; // fix for not clipping with scene (potential) floor
        private readonly NoiseGeneratorCache noiseGenCache = new ();
        private readonly TimeProfiler timeProfiler;

        private int parcelSize;
        private TerrainGenerationData terrainGenData;

        private TerrainFactory factory;
        private TerrainChunkDataGenerator chunkDataGenerator;
        private TerrainBoundariesGenerator boundariesGenerator;

        private Transform rootGo;
        private Transform ocean;

        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;
        private NativeParallelHashMap<int2, int> emptyParcelsData;
        private NativeList<int2> emptyParcels;
        private NativeParallelHashSet<int2> ownedParcels;

        private readonly List<Terrain> terrains = new ();
        public bool IsInitialized { get; private set; }

        public WorldTerrainGenerator(bool measureTime = false)
        {
            timeProfiler = new TimeProfiler(measureTime);
        }

        public void Dispose()
        {
            if (!IsInitialized) return;

            if (rootGo != null)
                UnityObjectUtils.SafeDestroy(rootGo);
        }

        public void Initialize(TerrainGenerationData terrainGenData)
        {
            this.terrainGenData = terrainGenData;

            parcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
            boundariesGenerator = new TerrainBoundariesGenerator(factory, parcelSize);
            chunkDataGenerator = new TerrainChunkDataGenerator(null, timeProfiler, terrainGenData, ReportCategory.LANDSCAPE, noiseGenCache);

            IsInitialized = true;
        }

        public void SwitchVisibility(bool isVisible)
        {
            if (!IsInitialized) return;

            if (rootGo != null)
                rootGo.gameObject.SetActive(isVisible);
        }

        public async UniTask GenerateTerrainAsync(NativeParallelHashSet<int2> ownedParcels, uint worldSeed = 1, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) return;

            this.ownedParcels = ownedParcels;
            var worldModel = new WorldModel(ownedParcels);
            var terrainModel = new TerrainModel(parcelSize, worldModel, terrainGenData.borderPadding + Mathf.RoundToInt(0.1f * (worldModel.SizeInParcels.x + worldModel.SizeInParcels.y) / 2f));

            rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
            rootGo.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

            factory.CreateOcean(rootGo);

            boundariesGenerator.SpawnCliffs(terrainModel.MinInUnits, terrainModel.MaxInUnits);
            boundariesGenerator.SpawnBorderColliders(terrainModel.MinInUnits, terrainModel.MaxInUnits, terrainModel.SizeInUnits);

            TerrainGenerationUtils.ExtractEmptyParcels(terrainModel, ref emptyParcels, ref ownedParcels);
            await SetupEmptyParcelDataAsync(cancellationToken, terrainModel);

            // Generate TerrainData's
            chunkDataGenerator.Prepare((int)worldSeed, parcelSize, ref emptyParcelsData, ref emptyParcelsNeighborData);

            foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
            {
                await GenerateTerrainDataAsync(chunkModel, terrainModel, worldSeed, cancellationToken);
                await UniTask.Yield(cancellationToken);
            }

            // Generate Terrain GameObjects
            terrains.Clear();
            foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
                terrains.Add(factory.CreateTerrainObject(chunkModel.TerrainData, rootGo, chunkModel.MinParcel * parcelSize, terrainGenData.terrainMaterial));

            await TerrainGenerationUtils.AddColorMapRendererAsync(rootGo, terrains, factory);

            // waiting a frame to create the color map renderer created a new bug where some stones do not render properly, this should fix it
            foreach (Terrain terrain in terrains)
                terrain.enabled = false;

            await UniTask.Yield();

            foreach (Terrain terrain in terrains)
                terrain.enabled = true;

            noiseGenCache.Dispose();
            emptyParcelsNeighborData.Dispose();
            emptyParcelsData.Dispose();
            emptyParcels.Dispose();
        }

        private async UniTask GenerateTerrainDataAsync(ChunkModel chunkModel, TerrainModel terrainModel, uint worldSeed, CancellationToken cancellationToken)
        {
            chunkModel.TerrainData = factory.CreateTerrainData(terrainModel.ChunkSizeInUnits, 0.1f);

            var tasks = new List<UniTask>
            {
                chunkDataGenerator.SetHeightsAsync(chunkModel.MinParcel, GetMaxHeightIndex(emptyParcelsData), parcelSize, chunkModel.TerrainData, worldSeed, cancellationToken, useCache: false),
                chunkDataGenerator.SetTexturesAsync(chunkModel.MinParcel.x * parcelSize, chunkModel.MinParcel.y * parcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken, false),
                chunkDataGenerator.SetDetailsAsync(chunkModel.MinParcel.x * parcelSize, chunkModel.MinParcel.y * parcelSize, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken, true, chunkModel.MinParcel, chunkModel.OccupiedParcels, false),
                chunkDataGenerator.SetTreesAsync(chunkModel.MinParcel, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken, useCache: false),
            };

            await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);

            chunkModel.TerrainData.SetHoles(0, 0, chunkDataGenerator.DigHoles(terrainModel, chunkModel, parcelSize, withOwned: false));
        }

        private async UniTask SetupEmptyParcelDataAsync(CancellationToken cancellationToken, TerrainModel terrainModel)
        {
            JobHandle handle = TerrainGenerationUtils.SetupEmptyParcelsJobs(
                ref emptyParcelsData, ref emptyParcelsNeighborData,
                emptyParcels.AsArray(), ref ownedParcels,
                terrainModel.MinParcel, terrainModel.MaxParcel,
                terrainGenData.heightScaleNerf);

            await handle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
        }

        // Calculate this outside the empty parcels Height jobs since they are Parallel
        private static int GetMaxHeightIndex(in NativeParallelHashMap<int2, int> emptyParcelsData)
        {
            int maxHeight = int.MinValue;

            foreach (KeyValue<int2, int> emptyParcelHeight in emptyParcelsData)
                if (emptyParcelHeight.Value > maxHeight)
                    maxHeight = emptyParcelHeight.Value;

            return maxHeight;
        }
    }
}
