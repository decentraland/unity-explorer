using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.PlacesAPIService;
using DCL.UI;
using DG.Tweening;
using ECS;
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
        private const MapLayer RENDER_LAYERS = MapLayer.SatelliteAtlas | MapLayer.ParcelsAtlas | MapLayer.PlayerMarker | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers;
        private const float ANIMATION_TIME = 0.2f;

        public readonly BridgeSystemBinding<TrackPlayerPositionSystem> SystemBinding;
        private readonly IMapRenderer mapRenderer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private CancellationTokenSource cts;
        private bool isCollapsed;

        private MapRendererTrackPlayerPosition mapRendererTrackPlayerPosition;
        private IMapCameraController mapCameraController;
        private Vector2Int previousParcelPosition;
        private SideMenuController sideMenuController;
        private static readonly int EXPAND = Animator.StringToHash("Expand");
        private static readonly int COLLAPSE = Animator.StringToHash("Collapse");
        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter { BackgroundIsActive = false } } };

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public MinimapController(
            ViewFactoryMethod viewFactory,
            IMapRenderer mapRenderer,
            IMVCManager mvcManager,
            IPlacesAPIService placesAPIService,
            TrackPlayerPositionSystem system,
            IRealmData realmData
        ) : base(viewFactory)
        {
            this.mapRenderer = mapRenderer;
            this.mvcManager = mvcManager;
            this.placesAPIService = placesAPIService;
            SystemBinding = AddModule(new BridgeSystemBinding<TrackPlayerPositionSystem>(this, QueryPlayerPositionQuery, system));
            this.realmData = realmData;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.expandMinimapButton.onClick.AddListener(ExpandMinimap);
            viewInstance.collapseMinimapButton.onClick.AddListener(CollapseMinimap);
            viewInstance.minimapRendererButton.Button.onClick.AddListener(() => mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Navmap))).Forget());
            viewInstance.sideMenuButton.onClick.AddListener(OpenSideMenu);
            viewInstance.SideMenuCanvasGroup.alpha = 0;
            viewInstance.SideMenuCanvasGroup.gameObject.SetActive(false);
            sideMenuController = new SideMenuController(viewInstance.sideMenuView);
        }

        private void ExpandMinimap()
        {
            if (!isCollapsed)
                return;

            viewInstance.collapseMinimapButton.gameObject.SetActive(true);
            viewInstance.expandMinimapButton.gameObject.SetActive(false);
            viewInstance.minimapRendererButton.gameObject.SetActive(true);
            viewInstance.minimapAnimator.SetTrigger(EXPAND);
            isCollapsed = false;
        }

        private void CollapseMinimap()
        {
            if (isCollapsed)
                return;

            viewInstance.collapseMinimapButton.gameObject.SetActive(false);
            viewInstance.expandMinimapButton.gameObject.SetActive(true);
            viewInstance.minimapRendererButton.gameObject.SetActive(false);
            viewInstance.minimapAnimator.SetTrigger(COLLAPSE);
            isCollapsed = true;
        }

        private void OpenSideMenu()
        {
            if (viewInstance.SideMenuCanvasGroup.gameObject.activeInHierarchy)
            {
                viewInstance.SideMenuCanvasGroup.DOFade(0, ANIMATION_TIME).SetEase(Ease.InOutQuad).OnComplete(() => viewInstance.SideMenuCanvasGroup.gameObject.gameObject.SetActive(false));
            }
            else
            {
                viewInstance.SideMenuCanvasGroup.gameObject.gameObject.SetActive(true);
                viewInstance.SideMenuCanvasGroup.DOFade(1, ANIMATION_TIME).SetEase(Ease.InOutQuad);
            }
        }

        [All(typeof(PlayerComponent))]
        [Query]
        private void QueryPlayerPosition(in CharacterTransform transformComponent)
        {
            Vector3 position = transformComponent.Position;

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

            SetWorldMode(realmData.ScenesAreFixed);
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
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            RetrieveParcelInfoAsync(playerParcelPosition).Forget();
            return;

            async UniTaskVoid RetrieveParcelInfoAsync(Vector2Int playerParcelPosition)
            {
                try
                {
                    PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(playerParcelPosition, cts.Token);
                    viewInstance.placeNameText.text = placeInfo?.title ?? "Unknown place";
                }
                catch (NotAPlaceException notAPlaceException)
                {
                    viewInstance.placeNameText.text = "Unknown place";
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Not a place requested: {notAPlaceException.Message}");
                }
                catch (Exception) { viewInstance.placeNameText.text = "Unknown place"; }
                finally { viewInstance.placeCoordinatesText.text = playerParcelPosition.ToString().Replace("(", "").Replace(")", ""); }
            }
        }

        private void SetWorldMode(bool isWorld)
        {
            viewInstance.nonWorldContainer.SetActive(!isWorld);
            viewInstance.goToGenesisCityButton.gameObject.SetActive(isWorld);

            if (isWorld)
                CollapseMinimap();
            else
                ExpandMinimap();
        }

        public override void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayerPositionSystem : ControllerECSBridgeSystem
    {
        internal TrackPlayerPositionSystem(World world) : base(world) { }
    }
}
