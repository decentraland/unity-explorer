using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Abstract;
using UnityEngine;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class LandscapeSatelliteViewSystem : BaseUnityLoopSystem
    {
        private const int PARCEL_SIZE = 16;
        private const int CHUNK_SIZE = 40;
        private const int GENESIS_HALF_PARCEL_WIDTH = 150;
        private const int SATELLITE_MAP_RESOLUTION = 8;
        private const float Z_FIGHT_THRESHOLD = 0.02f;

        private readonly LandscapeData landscapeData;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly Transform landscapeParentObject;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private bool isViewRendered;

        private LandscapeSatelliteViewSystem(World world,
            LandscapeData landscapeData,
            MapRendererTextureContainer textureContainer) : base(world)
        {
            this.landscapeData = landscapeData;
            this.textureContainer = textureContainer;
            landscapeParentObject = new GameObject("Satellite View").transform;
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        protected override void Update(float t)
        {
            if (textureContainer.IsComplete() && !isViewRendered)
                InitializeSatelliteView();
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
                }
            }

            isViewRendered = true;
        }
    }
}
