using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCLServices.MapRenderer;
using DCLServices.MapRenderer.CommonBehavior;
using DCLServices.MapRenderer.ConsumerUtils;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using DCLServices.MapRenderer.MapLayers.PlayerMarker;
using ECS.Unity.Transforms.Components;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Minimap
{
    public partial class MinimapController : ControllerBase<MinimapView>, IMapActivityOwner
    {
        private const MapLayer RENDER_LAYERS = MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.HomePoint | MapLayer.PlayerMarker | MapLayer.HotUsersMarkers | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.Friends;
        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter {BackgroundIsActive = false} } };

        private readonly IMapRenderer mapRenderer;
        private TrackPlayerPositionSystem trackPlayerPositionSystem;

        private MapRendererTrackPlayerPosition mapRendererTrackPlayerPosition;
        private IMapCameraController mapCameraController;


        public MinimapController(
            ViewFactoryMethod viewFactory,
            IMapRenderer mapRenderer
            ) : base(viewFactory)
        {
            this.mapRenderer = mapRenderer;
        }

        public void InjectSystem(TrackPlayerPositionSystem trackPlayerPositionSystem)
        {
            this.trackPlayerPositionSystem = trackPlayerPositionSystem;
            trackPlayerPositionSystem.SetQueryMethod(QueryPlayerPositionQuery);
            InitializeSystem();
        }

        [All(typeof(PlayerComponent))]
        [Query]
        private void QueryPlayerPosition(in TransformComponent transformComponent)
        {
            if (mapCameraController == null)
            {
                mapCameraController = mapRenderer.RentCamera(new MapCameraInput(
                    this,
                    RENDER_LAYERS,
                    Vector2Int.RoundToInt(MapRendererTrackPlayerPosition.GetPlayerCentricCoords(transformComponent.Transform.position)),
                    1,
                    viewInstance.pixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
                    new Vector2Int(viewInstance.mapRendererVisibleParcels, viewInstance.mapRendererVisibleParcels)
                ));

                mapRendererTrackPlayerPosition = new MapRendererTrackPlayerPosition(mapCameraController);
                viewInstance.mapRendererTargetImage.texture = mapCameraController.GetRenderTexture();
                viewInstance.pixelPerfectMapRendererTextureProvider.Activate(mapCameraController);
            }
            else
            {
                mapRendererTrackPlayerPosition.OnPlayerPositionChanged(transformComponent.Transform.position);
            }
        }

        public override CanvasOrdering.SortingLayer SortLayers => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntent(CancellationToken ct) => UniTask.CompletedTask;

        private void InitializeSystem()
        {
            trackPlayerPositionSystem.Activate();
            trackPlayerPositionSystem.InvokeQuery();
        }

        public override void OnBlur()
        {
            trackPlayerPositionSystem.Deactivate();
        }

        public override void OnFocus()
        {
            trackPlayerPositionSystem.Activate();
        }
    }

    //Once the realm changes the world is destroyed and it breaks
    //the lifecycle of this, controllers are never destroyed
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayerPositionSystem : ControllerECSBridgeSystem
    {
        public TrackPlayerPositionSystem(World world) : base(world)
        {
        }
    }
}
