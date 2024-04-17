using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    /// <summary>
    ///     This system is the one that creates the ground textures for the satellite view, also manages their visibility status based on the settings data
    /// </summary>
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeSatelliteSystem : BaseUnityLoopSystem
    {
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");

        private const int CHUNK_SIZE = 40;
        private const int GENESIS_HALF_PARCEL_WIDTH = 150;
        private const int SATELLITE_MAP_RESOLUTION = 8;
        private const float Z_FIGHT_THRESHOLD = 0.02f;

        private readonly int parcelSize;
        private readonly LandscapeData landscapeData;
        private readonly MapRendererTextureContainer textureContainer;
        private Transform landscapeParentObject;
        private MaterialPropertyBlock materialPropertyBlock;
        private readonly List<Renderer> satelliteRenderers = new ();

        private bool isViewRendered;
        private bool satelliteRenderersEnabled = true;

        private LandscapeSatelliteSystem(World world,
            LandscapeData landscapeData,
            MapRendererTextureContainer textureContainer) : base(world)
        {
            this.landscapeData = landscapeData;
            this.textureContainer = textureContainer;

            parcelSize = (int) ParcelMathHelper.PARCEL_SIZE;
        }

        public override void Initialize()
        {
            base.Initialize();

            landscapeParentObject = new GameObject("Satellite View").transform;
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        protected override void Update(float t)
        {
            if (textureContainer.IsComplete() && !isViewRendered)
                InitializeSatelliteView();

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

        private void InitializeSatelliteView()
        {
            int textureSize = CHUNK_SIZE * parcelSize;
            var genesisCityOffset = new Vector3(GENESIS_HALF_PARCEL_WIDTH * parcelSize, 0, GENESIS_HALF_PARCEL_WIDTH * parcelSize);

            // the map has some black weird margins, this is an approximation to fit the satellite view in place
            var mapTextureMargins = new Vector3(-2 * parcelSize, 0, -(20 * parcelSize) + 50 - 1.7f);

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
