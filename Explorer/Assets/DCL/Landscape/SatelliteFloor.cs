using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Utility;

namespace DCL.Landscape
{
    public class SatelliteFloor
    {
        private const string SATELLITE_FLOOR = "Satellite Floor";

        private const int PARCEL_SIZE = (int)ParcelMathHelper.PARCEL_SIZE;
        private const int CHUNK_SIZE = 40;
        private const int TEXTURE_SIZE = CHUNK_SIZE * PARCEL_SIZE;

        private const int GENESIS_HALF_PARCEL_WIDTH = 150;
        private const int SATELLITE_MAP_RESOLUTION = 8;
        private const float Z_FIGHT_THRESHOLD = 0.02f;

        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");

        private Renderer[] satelliteRenderers;
        private LandscapeData landscapeData;

        private bool satelliteRenderersEnabled;
        private bool initialized;

        public void Initialize(LandscapeData config)
        {
            landscapeData = config;

            satelliteRenderers = new Renderer[SATELLITE_MAP_RESOLUTION * SATELLITE_MAP_RESOLUTION];
        }

        public void Create(MapRendererTextureContainer textureContainer)
        {
            var materialPropertyBlock = new MaterialPropertyBlock();

            var genesisCityOffset = new Vector3(GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE, 0, GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE);
            var landscapeRootObject = new GameObject(SATELLITE_FLOOR).transform;

            // the map has some black weird margins, this is an approximation to fit the satellite floor in place
            var mapTextureMargins = new Vector3(-2 * PARCEL_SIZE, 0, -(20 * PARCEL_SIZE) + 50 - 1.7f);
            var quadCenter = new Vector3(TEXTURE_SIZE / 2f, 0, TEXTURE_SIZE / 2f);

            for (var x = 0; x < SATELLITE_MAP_RESOLUTION; x++)
            for (var y = 0; y < SATELLITE_MAP_RESOLUTION; y++)
            {
                Transform groundTile = Object.Instantiate(landscapeData.mapChunk,landscapeRootObject);
                groundTile.name = $"{SATELLITE_FLOOR} {x},{y}";

                // Position and rotation
                int posX = x * TEXTURE_SIZE;
                int posZ = y * TEXTURE_SIZE;
                Vector3 position = new Vector3(posX, -Z_FIGHT_THRESHOLD, posZ) - genesisCityOffset + quadCenter + mapTextureMargins ;
                groundTile.SetPositionAndRotation(position, Quaternion.Euler(90, 0, 0));

                // Rendering
                materialPropertyBlock.SetTexture(BASE_MAP, textureContainer.GetChunk(new Vector2Int(x, SATELLITE_MAP_RESOLUTION - 1 - y)));
                Renderer satelliteRenderer = groundTile.GetComponent<Renderer>();
                satelliteRenderer.SetPropertyBlock(materialPropertyBlock);

                satelliteRenderers[x + (y * SATELLITE_MAP_RESOLUTION)] = satelliteRenderer;
            }

            SwitchVisibilityAsync(landscapeData.showSatelliteView).Forget();

            initialized = true;
        }

        public async UniTask SwitchVisibilityAsync(bool isVisible)
        {
            if (satelliteRenderersEnabled == isVisible) return;

            if (!initialized)
                await UniTask.WaitUntil(() => initialized);

            satelliteRenderersEnabled = isVisible;

            foreach (Renderer satelliteRenderer in satelliteRenderers)
                satelliteRenderer.forceRenderingOff = !isVisible;
        }
    }
}
