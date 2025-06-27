using DCL.Landscape.Settings;
using DCL.Utilities;
using System;
using Unity.Mathematics;
using UnityEngine;
using TerrainData = Decentraland.Terrain.TerrainData;

namespace DCL.Landscape
{
    public sealed class WorldTerrainGenerator : IContainParcel
    {
        private const string TERRAIN_OBJECT_NAME = "World Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.001f; // fix for not clipping with scene (potential) floor

        private int parcelSize;
        private TerrainGenerationData terrainGenData;
        private TerrainData terrainData;
        private TerrainFactory factory;
        private TerrainBoundariesGenerator boundariesGenerator;

        private Transform rootGo;
        private Transform ocean;

        public bool IsInitialized { get; private set; }

        private TerrainModel terrainModel;

        public WorldTerrainGenerator()
        {
        }

        public bool Contains(Vector2Int parcel)
        {
            if (IsInitialized)
                return terrainModel.IsInsideBounds(parcel);

            return false;
        }

        public void Initialize(TerrainGenerationData terrainGenData, TerrainData terrainData)
        {
            this.terrainGenData = terrainGenData;
            this.terrainData = terrainData;

            parcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
            boundariesGenerator = new TerrainBoundariesGenerator(factory, parcelSize);

            IsInitialized = true;
        }

        public void Hide()
        {
            if (!IsInitialized) return;

            if (rootGo != null)
                rootGo.gameObject.SetActive(false);
        }

        public void GenerateTerrain(int2[] occupied, AsyncLoadProcessReport processReport = null)
        {
            if (!IsInitialized) return;

            int2[] none = Array.Empty<int2>();

            terrainModel = new TerrainModel(occupied, none, none, parcelSize,
                terrainGenData.borderPadding, 0.05f);

            rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
            rootGo.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

            factory.CreateOcean(rootGo);

            boundariesGenerator.SpawnCliffs(terrainModel.MinInUnits, terrainModel.MaxInUnits);
            boundariesGenerator.SpawnBorderColliders(terrainModel.MinInUnits, terrainModel.MaxInUnits, terrainModel.SizeInUnits);

            terrainModel.UpdateTerrainData(terrainData);

            processReport?.SetProgress(1f);
        }
    }
}
