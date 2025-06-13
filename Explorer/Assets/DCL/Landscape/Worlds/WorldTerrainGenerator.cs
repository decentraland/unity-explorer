using Cysharp.Threading.Tasks;
using DCL.Landscape.Settings;
using DCL.Utilities;
using System;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class WorldTerrainGenerator : IDisposable, IContainParcel
    {
        private const string TERRAIN_OBJECT_NAME = "World Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.001f; // fix for not clipping with scene (potential) floor

        private int parcelSize;
        private TerrainGenerationData terrainGenData;

        private TerrainFactory factory;
        private TerrainBoundariesGenerator boundariesGenerator;

        private Transform rootGo;
        private Transform ocean;

        private NativeParallelHashMap<int2, int> emptyParcelsData;
        private NativeList<int2> emptyParcels;

        public bool IsInitialized { get; private set; }

        private TerrainModel terrainModel;

        public WorldTerrainGenerator(bool measureTime = false)
        {
        }

        public void Dispose()
        {
            // If we destroy rootGo here it causes issues on application exit
        }

        public bool Contains(Vector2Int parcel)
        {
            if (IsInitialized)
                return terrainModel.IsInsideBounds(parcel);

            return false;
        }

        public void Initialize(TerrainGenerationData terrainGenData)
        {
            this.terrainGenData = terrainGenData;

            parcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
            boundariesGenerator = new TerrainBoundariesGenerator(factory, parcelSize);

            IsInitialized = true;
        }

        public void SwitchVisibility(bool isVisible)
        {
            if (!IsInitialized) return;

            if (rootGo != null)
                rootGo.gameObject.SetActive(isVisible);
        }

        public async UniTask GenerateTerrainAsync(NativeParallelHashSet<int2> ownedParcels, uint worldSeed = 1,
            AsyncLoadProcessReport processReport = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) return;

            var worldModel = new WorldModel(ownedParcels);
            terrainModel = new TerrainModel(parcelSize, worldModel, terrainGenData.borderPadding + Mathf.RoundToInt(0.1f * (worldModel.SizeInParcels.x + worldModel.SizeInParcels.y) / 2f));

            rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
            rootGo.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

            factory.CreateOcean(rootGo);

            boundariesGenerator.SpawnCliffs(terrainModel.MinInUnits, terrainModel.MaxInUnits);
            boundariesGenerator.SpawnBorderColliders(terrainModel.MinInUnits, terrainModel.MaxInUnits, terrainModel.SizeInUnits);

            if (processReport != null) processReport.SetProgress(0.5f);

            FreeMemory();
            processReport?.SetProgress(1f);
        }

        private void FreeMemory()
        {
            emptyParcelsData.Dispose();
            emptyParcels.Dispose();
        }
    }
}
