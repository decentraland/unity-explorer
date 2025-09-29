﻿using Cysharp.Threading.Tasks;
using DCL.Landscape.Settings;
using DCL.Utilities;
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    /// <summary>
    ///     Based on the old terrain
    /// </summary>
    public class WorldTerrainGenerator : IDisposable, ITerrain
    {
        private const string TERRAIN_OBJECT_NAME = "World Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.001f; // fix for not clipping with scene (potential) floor

        private int parcelSize;
        private TerrainGenerationData terrainGenData;
        public TreeData? Trees { get; private set; }

        private TerrainFactory factory;
        private TerrainBoundariesGenerator boundariesGenerator;

        private Transform rootGo;
        public bool IsInitialized { get; private set; }
        public bool IsTerrainShown { get; private set; }

        public TerrainModel? TerrainModel { get; private set; }
        public Texture2D? OccupancyMap { get; private set; }
        public NativeArray<byte> OccupancyMapData { get; private set; }
        public int OccupancyMapSize { get; private set; }
        public int OccupancyFloor { get; private set; }
        public float MaxHeight { get; private set; }

        public void Dispose()
        {
            // If we destroy rootGo here it causes issues on application exit
        }

        public bool Contains(Vector2Int parcel)
        {
            if (TerrainModel != null)
                return TerrainModel.IsInsideBounds(parcel);

            return false;
        }

        public float GetHeight(float x, float z) =>
            TerrainGenerator.GetParcelNoiseHeight(x, z, OccupancyMapData, OccupancyMapSize,
                terrainGenData.parcelSize, OccupancyFloor, MaxHeight);

        public async UniTask InitializeAsync(TerrainGenerationData terrainGenData, int[] treeRendererKeys)
        {
            this.terrainGenData = terrainGenData;

            parcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
            boundariesGenerator = new TerrainBoundariesGenerator(factory, parcelSize);
            Trees = new TreeData(treeRendererKeys, terrainGenData);
            await Trees.LoadAsync($"{Application.streamingAssetsPath}/WorldsTrees.bin");
            IsInitialized = true;
        }

        public void SwitchVisibility(bool isVisible)
        {
            if (!IsInitialized) return;

            IsTerrainShown = isVisible;

            if (rootGo != null)
                rootGo.gameObject.SetActive(isVisible);
        }

        public void GenerateTerrain(NativeParallelHashSet<int2> ownedParcels,
            AsyncLoadProcessReport? processReport = null)
        {
            if (!IsInitialized) return;

            var worldModel = new WorldModel(ownedParcels);
            TerrainModel = new TerrainModel(parcelSize, worldModel, terrainGenData.borderPadding + Mathf.RoundToInt(0.1f * (worldModel.SizeInParcels.x + worldModel.SizeInParcels.y) / 2f));

            rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
            rootGo.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

            factory.CreateOcean(rootGo);

            boundariesGenerator.SpawnCliffs(TerrainModel.MinInUnits, TerrainModel.MaxInUnits);
            boundariesGenerator.SpawnBorderColliders(TerrainModel.MinInUnits, TerrainModel.MaxInUnits, TerrainModel.SizeInUnits);

            if (processReport != null) processReport.SetProgress(0.5f);

            OccupancyMap = TerrainGenerator.CreateOccupancyMap(ownedParcels, TerrainModel.MinParcel,
                TerrainModel.MaxParcel, 0);

            (int floor, int maxSteps) distanceFieldData = TerrainGenerator.WriteInteriorChamferOnWhite(OccupancyMap, TerrainModel.MinParcel, TerrainModel.MaxParcel, 0);
            OccupancyFloor = distanceFieldData.floor;
            MaxHeight = distanceFieldData.maxSteps * terrainGenData.stepHeight;

            OccupancyMap.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            OccupancyMapData = OccupancyMap.GetRawTextureData<byte>();
            OccupancyMapSize = OccupancyMap.width; // width == height

            Trees!.SetTerrainData(TerrainModel.MinParcel, TerrainModel.MaxParcel, OccupancyMapData,
                OccupancyMapSize, OccupancyFloor, MaxHeight);

            Trees.Instantiate();

            processReport?.SetProgress(1f);

            IsTerrainShown = true;
        }
    }
}
