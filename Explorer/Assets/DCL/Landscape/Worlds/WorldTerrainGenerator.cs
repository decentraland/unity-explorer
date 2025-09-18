using Cysharp.Threading.Tasks;
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

        private TerrainModel terrainModel;
        public Texture2D? OccupancyMap { get; private set; }
        public int OccupancyFloor { get; private set; }

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

        public float GetHeight(float x, float z) =>
            Physics.Raycast(new Vector3(x, 100, z), Vector3.down, out RaycastHit hit) ? hit.point.y : z;

        public async UniTask Initialize(TerrainGenerationData terrainGenData, int[] treeRendererKeys)
        {
            this.terrainGenData = terrainGenData;

            parcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
            boundariesGenerator = new TerrainBoundariesGenerator(factory, parcelSize);
            Trees = new TreeData(treeRendererKeys, terrainGenData);
            await Trees!.LoadAsync($"{Application.streamingAssetsPath}/WorldsTrees.bin");
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
            terrainModel = new TerrainModel(parcelSize, worldModel, terrainGenData.borderPadding + Mathf.RoundToInt(0.1f * (worldModel.SizeInParcels.x + worldModel.SizeInParcels.y) / 2f));

            rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
            rootGo.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

            factory.CreateOcean(rootGo);

            boundariesGenerator.SpawnCliffs(terrainModel.MinInUnits, terrainModel.MaxInUnits);
            boundariesGenerator.SpawnBorderColliders(terrainModel.MinInUnits, terrainModel.MaxInUnits, terrainModel.SizeInUnits);

            if (processReport != null) processReport.SetProgress(0.5f);

            OccupancyMap = TerrainGenerator.CreateOccupancyMap(ownedParcels, terrainModel.MinParcel,
                terrainModel.MaxParcel, 0);

            OccupancyFloor = TerrainGenerator.WriteInteriorChamferOnWhite(OccupancyMap,
                terrainModel.MinParcel, terrainModel.MaxParcel, 0);

            OccupancyMap.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Trees!.SetTerrainData(terrainModel.MinParcel, terrainModel.MaxParcel, OccupancyMap,
                OccupancyFloor);

            Trees.Instantiate();

            processReport?.SetProgress(1f);

            IsTerrainShown = true;
        }
    }
}
