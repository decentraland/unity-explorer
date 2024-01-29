using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Landscape.Components;
using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeParcelInitializerSystem : BaseUnityLoopSystem
    {
        private const int PARCEL_SIZE = 16;
        private const int CHUNK_SIZE = 40;
        private const int GENESIS_HALF_PARCEL_WIDTH = 150;
        private readonly LandscapeData landscapeData;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly Transform landscapeParentObject;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private bool disableSatellite;

        private LandscapeParcelInitializerSystem(World world,
            LandscapeData landscapeData,
            MapRendererTextureContainer textureContainer) : base(world)
        {
            this.landscapeData = landscapeData;
            this.textureContainer = textureContainer;
            landscapeParentObject = new GameObject("Landscape").transform;
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        protected override void Update(float t)
        {
            if (textureContainer.IsComplete())
                InitializeMapChunksQuery(World);

            if (disableSatellite != landscapeData.disableSatelliteView)
            {
                disableSatellite = landscapeData.disableSatelliteView;
                UpdateSatelliteViewsQuery(World);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateSatelliteViews(ref SatelliteView satelliteView)
        {
            for (var i = 0; i < satelliteView.renderers.Length; i++)
                satelliteView.renderers[i].forceRenderingOff = landscapeData.disableSatelliteView;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(LandscapeParcelInitialization))]
        private void InitializeMapChunks(in Entity entity, ref SatelliteView satelliteView)
        {
            var genesisCityOffset = new Vector3(GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE, 0, GENESIS_HALF_PARCEL_WIDTH * PARCEL_SIZE);
            var mapTextureMargins = new Vector3(-2 * PARCEL_SIZE, 0, -(20 * PARCEL_SIZE) + 50 - 1.7f);
            var quadCenter = new Vector3(320, 0, 320);
            Vector3 zFightPrevention = Vector3.down * 0.015f;

            satelliteView.renderers = new Renderer[8 * 8];

            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    int posX = x * CHUNK_SIZE * PARCEL_SIZE;
                    int posZ = y * CHUNK_SIZE * PARCEL_SIZE;

                    Vector3 coord = new Vector3(posX, 0, posZ) - genesisCityOffset + quadCenter + mapTextureMargins + zFightPrevention;

                    Transform groundTile = Object.Instantiate(landscapeData.mapChunk, landscapeParentObject, true);
                    groundTile.position = coord;
                    groundTile.eulerAngles = new Vector3(90, 0, 0);

                    materialPropertyBlock.SetTexture(BASE_MAP, textureContainer.GetChunk(new Vector2Int(x, 7 - y)));
                    Renderer satelliteRenderer = groundTile.GetComponent<Renderer>();
                    satelliteRenderer.SetPropertyBlock(materialPropertyBlock);
                    groundTile.name = $"CHUNK {x},{y}";
                    satelliteView.renderers[x + (y * 8)] = satelliteRenderer;
                }
            }

            World.Remove<LandscapeParcelInitialization>(entity);
        }
    }
}
