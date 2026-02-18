using Cysharp.Threading.Tasks;
using DCL.Landscape.Settings;
using DCL.Utilities;
using System;
using System.Collections.Generic;
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

        public int ParcelSize { get; private set; }
        private TerrainGenerationData terrainGenData;
        public TreeData? Trees { get; private set; }

        private TerrainFactory factory;
        private TerrainBoundariesGenerator boundariesGenerator;

        private Transform rootGo;
        public bool IsInitialized { get; private set; }
        public bool IsTerrainShown { get; private set; }
        private LandscapeData landscapeData;
        public TerrainModel? TerrainModel { get; private set; }
        public Texture2D? OccupancyMap { get; private set; }
        public NativeArray<byte> OccupancyMapData { get; private set; }
        public int OccupancyMapSize { get; private set; }
        public int OccupancyFloor { get; private set; }
        public float MaxHeight { get; private set; }
        public IReadOnlyList<Transform> Cliffs { get; private set; }

        public void Dispose()
        {
            // If we destroy rootGo here it causes issues on application exit
        }

        public async UniTask InitializeAsync(TerrainGenerationData terrainGenData,
            int[] treeRendererKeys, LandscapeData landscapeData)
        {
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:47"); // SPECIAL_DEBUG_LINE_STATEMENT
            this.terrainGenData = terrainGenData;
            this.landscapeData = landscapeData;
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:50"); // SPECIAL_DEBUG_LINE_STATEMENT
            ParcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:53"); // SPECIAL_DEBUG_LINE_STATEMENT
            boundariesGenerator = new TerrainBoundariesGenerator(factory, ParcelSize);
            Trees = new TreeData(treeRendererKeys, terrainGenData);
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:56"); // SPECIAL_DEBUG_LINE_STATEMENT
            await Trees.LoadAsync($"{Application.streamingAssetsPath}/WorldsTrees.bin");
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:58"); // SPECIAL_DEBUG_LINE_STATEMENT
            IsInitialized = true;
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:60"); // SPECIAL_DEBUG_LINE_STATEMENT
        }

        public int GetChunkSize() =>
            terrainGenData.chunkSize;

        public void SwitchVisibility(bool isVisible)
        {
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:68"); // SPECIAL_DEBUG_LINE_STATEMENT
            if (!IsInitialized) return;

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:71"); // SPECIAL_DEBUG_LINE_STATEMENT
            IsTerrainShown = isVisible;

            if (rootGo != null)
                rootGo.gameObject.SetActive(isVisible);
        }

        public void GenerateTerrain(NativeHashSet<int2>.ReadOnly ownedParcels,
            AsyncLoadProcessReport? processReport = null)
        {
UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:81"); // SPECIAL_DEBUG_LINE_STATEMENT
            if (!IsInitialized) return;

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:84"); // SPECIAL_DEBUG_LINE_STATEMENT
            var worldModel = new WorldModel(ownedParcels);
            TerrainModel = new TerrainModel(ParcelSize, worldModel, terrainGenData.borderPadding + Mathf.RoundToInt(0.1f * (worldModel.SizeInParcels.x + worldModel.SizeInParcels.y) / 2f));

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:88"); // SPECIAL_DEBUG_LINE_STATEMENT
            rootGo = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
            rootGo.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:92"); // SPECIAL_DEBUG_LINE_STATEMENT
            factory.CreateOcean(rootGo);

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:95"); // SPECIAL_DEBUG_LINE_STATEMENT
            Cliffs = boundariesGenerator.SpawnCliffs(TerrainModel.MinInUnits, TerrainModel.MaxInUnits);
            boundariesGenerator.SpawnBorderColliders(TerrainModel.MinInUnits, TerrainModel.MaxInUnits, TerrainModel.SizeInUnits);

            if (processReport != null) processReport.SetProgress(0.5f);

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:101"); // SPECIAL_DEBUG_LINE_STATEMENT
            OccupancyMap = TerrainGenerator.CreateOccupancyMap(ownedParcels, TerrainModel.MinParcel,
                TerrainModel.MaxParcel, 0);

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:105"); // SPECIAL_DEBUG_LINE_STATEMENT
            (int floor, int maxSteps) distanceFieldData = TerrainGenerator.WriteInteriorChamferOnWhite(OccupancyMap, TerrainModel.MinParcel, TerrainModel.MaxParcel, 0);
            OccupancyFloor = distanceFieldData.floor;
            MaxHeight = distanceFieldData.maxSteps * terrainGenData.stepHeight;

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:110"); // SPECIAL_DEBUG_LINE_STATEMENT
            OccupancyMap.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            OccupancyMapData = OccupancyMap.GetRawTextureData<byte>();
            OccupancyMapSize = OccupancyMap.width; // width == height

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:115"); // SPECIAL_DEBUG_LINE_STATEMENT
            Trees!.SetTerrainData(TerrainModel.MinParcel, TerrainModel.MaxParcel, OccupancyMapData,
                OccupancyMapSize, OccupancyFloor, MaxHeight);

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:119"); // SPECIAL_DEBUG_LINE_STATEMENT
            Trees.Instantiate();

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:122"); // SPECIAL_DEBUG_LINE_STATEMENT
            if (landscapeData.RenderTrees)
                Trees.Show();
            else
                Trees.Hide();

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:128"); // SPECIAL_DEBUG_LINE_STATEMENT
            processReport?.SetProgress(1f);

UnityEngine.Debug.Log("CALLED WorldTerrainGenerator.cs:131"); // SPECIAL_DEBUG_LINE_STATEMENT
            IsTerrainShown = true;
        }
    }
}
