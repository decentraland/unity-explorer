using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeViewSystem : BaseUnityLoopSystem
    {
        private const int PARCEL_SIZE = 16;
        private const int CHUNK_SIZE = 40;
        private const int GENESIS_HALF_PARCEL_WIDTH = 150;
        private const int SATELLITE_MAP_RESOLUTION = 8;
        private const float Z_FIGHT_THRESHOLD = 0.02f;

        private readonly LandscapeData landscapeData;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly TerrainGenerator terrainGenerator;
        private readonly Transform landscapeParentObject;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private bool isViewRendered;
        private readonly List<Renderer> satelliteRenderers = new ();
        private bool satelliteRenderersEnabled = true;

        private LandscapeViewSystem(World world,
            LandscapeData landscapeData,
            MapRendererTextureContainer textureContainer,
            TerrainGenerator terrainGenerator) : base(world)
        {
            this.landscapeData = landscapeData;
            this.textureContainer = textureContainer;
            this.terrainGenerator = terrainGenerator;
            landscapeParentObject = new GameObject("Satellite View").transform;
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        protected override void Update(float t)
        {
            if (textureContainer.IsComplete() && !isViewRendered)
                InitializeSatelliteView();

            if (terrainGenerator.IsTerrainGenerated())
                UpdateTerrainVisibilityQuery(World);

            UpdateSatelliteView();
        }

        private void UpdateSatelliteView()
        {
            if (satelliteRenderersEnabled != landscapeData.showSatelliteView)
            {
                satelliteRenderersEnabled = landscapeData.showSatelliteView;

                foreach (Renderer satelliteRenderer in satelliteRenderers)
                    satelliteRenderer.forceRenderingOff = !satelliteRenderersEnabled;
            }
        }

        [Query]
        private void UpdateTerrainVisibility(in Entity _, in CameraComponent cameraComponent)
        {
            Camera camera = cameraComponent.Camera;
            IReadOnlyList<Terrain> terrains = terrainGenerator.GetTerrains();

            for (var i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];

                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                Bounds bounds = GetTerrainBoundsInWorldSpace(terrain);
                bool isVisible = GeometryUtility.TestPlanesAABB(planes, bounds);

                terrain.drawHeightmap = isVisible && landscapeData.drawTerrain;
                terrain.drawTreesAndFoliage = isVisible && landscapeData.drawTerrainDetails;
            }
        }

        private Bounds GetTerrainBoundsInWorldSpace(Terrain terrain)
        {
            Bounds localBounds = terrain.terrainData.bounds;
            Vector3 terrainPosition = terrain.transform.position;
            var worldBounds = new Bounds(localBounds.center + terrainPosition, localBounds.size);
            return worldBounds;
        }

        private void InitializeSatelliteView()
        {
            int textureSize = CHUNK_SIZE * PARCEL_SIZE;
            var genesisCityOffset = new Vector3(GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE, 0, GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE);

            // the map has some black weird margins, this is an approximation to fit the satellite view in place
            var mapTextureMargins = new Vector3(-2 * PARCEL_SIZE, 0, -(20 * PARCEL_SIZE) + 50 - 1.7f);

            var quadCenter = new Vector3(textureSize * 0.5f, 0, textureSize * 0.5f);
            Vector3 zFightPrevention = Vector3.down * Z_FIGHT_THRESHOLD;

            for (var x = 0; x < SATELLITE_MAP_RESOLUTION; x++)
            {
                for (var y = 0; y < SATELLITE_MAP_RESOLUTION; y++)
                {
                    int posX = x * textureSize;
                    int posZ = y * textureSize;

                    Vector3 coord = new Vector3(posX, 0, posZ) - genesisCityOffset + quadCenter + mapTextureMargins + zFightPrevention;

                    Transform groundTile = Object.Instantiate(landscapeData.mapChunk, landscapeParentObject, true);
                    groundTile.position = coord;
                    groundTile.eulerAngles = new Vector3(90, 0, 0);

                    materialPropertyBlock.SetTexture(BASE_MAP, textureContainer.GetChunk(new Vector2Int(x, SATELLITE_MAP_RESOLUTION - 1 - y)));
                    Renderer satelliteRenderer = groundTile.GetComponent<Renderer>();
                    satelliteRenderer.SetPropertyBlock(materialPropertyBlock);
                    groundTile.name = $"SatelliteView {x},{y}";
                    satelliteRenderers.Add(satelliteRenderer);
                }
            }

            isViewRendered = true;
        }
    }
}
