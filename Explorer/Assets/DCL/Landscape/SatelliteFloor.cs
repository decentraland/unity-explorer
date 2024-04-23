using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Landscape
{
    public class SatelliteFloor
    {
        private const int PARCEL_SIZE = (int)ParcelMathHelper.PARCEL_SIZE;

        private const int CHUNK_SIZE = 40;
        private const int GENESIS_HALF_PARCEL_WIDTH = 150;
        private const int SATELLITE_MAP_RESOLUTION = 8;
        private const float Z_FIGHT_THRESHOLD = 0.02f;

        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");

        private Renderer[] satelliteRenderers;

        private Transform landscapeParentObject;
        private MaterialPropertyBlock materialPropertyBlock;
        private LandscapeData landscapeData;

        private bool satelliteRenderersEnabled;

        public void Initialize(LandscapeData config)
        {
            landscapeData = config;

            landscapeParentObject = new GameObject("Satellite View").transform;
            materialPropertyBlock = new MaterialPropertyBlock();

            satelliteRenderers = new Renderer[SATELLITE_MAP_RESOLUTION * SATELLITE_MAP_RESOLUTION];
        }

        public void Create(MapRendererTextureContainer textureContainer)
        {
            int textureSize = CHUNK_SIZE * PARCEL_SIZE;
            var genesisCityOffset = new Vector3(GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE, 0, GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE);

            // the map has some black weird margins, this is an approximation to fit the satellite view in place
            var mapTextureMargins = new Vector3(-2 * PARCEL_SIZE, 0, -(20 * PARCEL_SIZE) + 50 - 1.7f);

            var quadCenter = new Vector3(textureSize * 0.5f, 0, textureSize * 0.5f);
            Vector3 zFightPrevention = Vector3.down * Z_FIGHT_THRESHOLD;

            for (var x = 0; x < SATELLITE_MAP_RESOLUTION; x++)
            for (var y = 0; y < SATELLITE_MAP_RESOLUTION; y++)
            {
                int posX = x * textureSize;
                int posZ = y * textureSize;

                Vector3 coord = new Vector3(posX, 0, posZ) - genesisCityOffset + quadCenter + mapTextureMargins + zFightPrevention;

                Transform groundTile = Object.Instantiate(landscapeData.mapChunk, landscapeParentObject, true);
                groundTile.name = $"SatelliteView {x},{y}";
                groundTile.SetPositionAndRotation(coord, Quaternion.Euler(90, 0, 0));

                materialPropertyBlock.SetTexture(BASE_MAP, textureContainer.GetChunk(new Vector2Int(x, SATELLITE_MAP_RESOLUTION - 1 - y)));

                Renderer satelliteRenderer = groundTile.GetComponent<Renderer>();
                satelliteRenderer.SetPropertyBlock(materialPropertyBlock);

                satelliteRenderers[x + (y * SATELLITE_MAP_RESOLUTION)] = satelliteRenderer;
            }

            SwitchVisibility(landscapeData.showSatelliteView);
        }

        public void SwitchVisibility(bool isVisible)
        {
            if (satelliteRenderersEnabled == isVisible) return;

            satelliteRenderersEnabled = isVisible;

            foreach (Renderer satelliteRenderer in satelliteRenderers)
                satelliteRenderer.forceRenderingOff = !isVisible;
        }
    }
}
