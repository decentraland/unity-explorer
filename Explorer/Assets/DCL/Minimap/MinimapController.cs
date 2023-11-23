using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.ExplorePanel;
using DCL.PlacesAPIService;
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
using Utility;

namespace DCL.Minimap
{
    public partial class MinimapController : ControllerBase<MinimapView>, IMapActivityOwner
    {
        private const MapLayer RENDER_LAYERS = MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.HomePoint | MapLayer.PlayerMarker | MapLayer.HotUsersMarkers | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.Friends;
        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter { BackgroundIsActive = false } } };

        public readonly BridgeSystemBinding<TrackPlayerPositionSystem> SystemBinding;
        private readonly IMapRenderer mapRenderer;
        private readonly MVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;

        private MapRendererTrackPlayerPosition mapRendererTrackPlayerPosition;
        private IMapCameraController mapCameraController;
        private Vector2Int previousParcelPosition;

        public MinimapController(
            ViewFactoryMethod viewFactory,
            IMapRenderer mapRenderer,
            MVCManager mvcManager,
            IPlacesAPIService placesAPIService
        ) : base(viewFactory)
        {
            this.mapRenderer = mapRenderer;
            this.mvcManager = mvcManager;
            this.placesAPIService = placesAPIService;
            SystemBinding = AddModule(new BridgeSystemBinding<TrackPlayerPositionSystem>(this, QueryPlayerPositionQuery));
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.expandMinimapButton.onClick.RemoveAllListeners();
            viewInstance.expandMinimapButton.onClick.AddListener(ExpandMinimap);
            viewInstance.minimapRendererButton.onClick.RemoveAllListeners();
            viewInstance.minimapRendererButton.onClick.AddListener(() => mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(null))).Forget());
        }

        private void ExpandMinimap() =>
            viewInstance.minimapContainer.gameObject.SetActive(!viewInstance.minimapContainer.gameObject.activeSelf);

        [All(typeof(PlayerComponent))]
        [Query]
        private void QueryPlayerPosition(in TransformComponent transformComponent)
        {
            var position = transformComponent.Transform.position;

            if (mapCameraController == null)
            {
                mapCameraController = mapRenderer.RentCamera(new MapCameraInput(
                    this,
                    RENDER_LAYERS,
                    Vector2Int.RoundToInt(MapRendererTrackPlayerPosition.GetPlayerCentricCoords(position)),
                    1,
                    viewInstance.pixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
                    new Vector2Int(viewInstance.mapRendererVisibleParcels, viewInstance.mapRendererVisibleParcels)
                ));

                mapRendererTrackPlayerPosition = new MapRendererTrackPlayerPosition(mapCameraController);
                viewInstance.mapRendererTargetImage.texture = mapCameraController.GetRenderTexture();
                viewInstance.pixelPerfectMapRendererTextureProvider.Activate(mapCameraController);
                GetPlaceInfoAsync(position);
            }
            else
            {
                mapRendererTrackPlayerPosition.OnPlayerPositionChanged(position);
                GetPlaceInfoAsync(position);
            }
        }

        protected override void OnBlur()
        {
            mapCameraController.SuspendRendering();
        }

        protected override void OnFocus()
        {
            mapCameraController.ResumeRendering();

            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, true);
            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, false);
        }

        private void GetPlaceInfoAsync(Vector3 playerPosition)
        {
            Vector2Int playerParcelPosition = ParcelMathHelper.WorldToGridPosition(playerPosition);

            if (previousParcelPosition == playerParcelPosition)
                return;

            previousParcelPosition = playerParcelPosition;

            RetrieveParcelInfo(playerParcelPosition).Forget();
            return;

            async UniTaskVoid RetrieveParcelInfo(Vector2Int playerParcelPosition)
            {
                try
                {
                    PlacesData.PlaceInfo placeInfo = await placesAPIService.GetPlace(playerParcelPosition, CancellationToken.None);
                    viewInstance.placeNameText.text = placeInfo.title;
                }
                catch (Exception) { viewInstance.placeNameText.text = "Unknown place"; }
                finally { viewInstance.placeCoordinatesText.text = playerParcelPosition.ToString(); }
            }
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            UniTask.CompletedTask;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayerPositionSystem : ControllerECSBridgeSystem
    {
        internal TrackPlayerPositionSystem(World world) : base(world) { }
    }
}
