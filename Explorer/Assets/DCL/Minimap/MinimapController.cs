using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.PlacesAPIService;
using DCL.UI;
using DG.Tweening;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
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
        private const MapLayer RENDER_LAYERS = MapLayer.SatelliteAtlas | MapLayer.PlayerMarker | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers | MapLayer.Pins | MapLayer.Path;
        private const float ANIMATION_TIME = 0.2f;
        private const string ORIGIN = "minimap";

        private readonly IMapRenderer mapRenderer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IRealmNavigator realmNavigator;
        private readonly IScenesCache scenesCache;
        private readonly IMapPathEventBus mapPathEventBus;

        private CancellationTokenSource cts;
        private MapRendererTrackPlayerPosition mapRendererTrackPlayerPosition;
        private IMapCameraController? mapCameraController;
        private Vector2Int previousParcelPosition;
        private SceneRestrictionsController sceneRestrictionsController;

        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter { BackgroundIsActive = false } } };

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public MinimapController(
            ViewFactoryMethod viewFactory,
            IMapRenderer mapRenderer,
            IMVCManager mvcManager,
            IPlacesAPIService placesAPIService,
            IRealmData realmData,
            IChatMessagesBus chatMessagesBus,
            IRealmNavigator realmNavigator,
            IScenesCache scenesCache,
            IMapPathEventBus mapPathEventBus
        ) : base(viewFactory)
        {
            this.mapRenderer = mapRenderer;
            this.mvcManager = mvcManager;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.chatMessagesBus = chatMessagesBus;
            this.realmNavigator = realmNavigator;
            this.scenesCache = scenesCache;
            this.mapPathEventBus = mapPathEventBus;
        }

        public void HookPlayerPositionTrackingSystem(TrackPlayerPositionSystem system) =>
            AddModule(new BridgeSystemBinding<TrackPlayerPositionSystem>(this, QueryPlayerPositionQuery, system));

        private void OnRealmChanged(bool isGenesis)
        {
            SetWorldMode(!isGenesis);
            previousParcelPosition = new Vector2Int(int.MaxValue, int.MaxValue);
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.expandMinimapButton.onClick.AddListener(ExpandMinimap);
            viewInstance.collapseMinimapButton.onClick.AddListener(CollapseMinimap);
            viewInstance.minimapRendererButton.Button.onClick.AddListener(() => mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Navmap))).Forget());
            viewInstance.sideMenuButton.onClick.AddListener(OpenSideMenu);
            viewInstance.goToGenesisCityButton.onClick.AddListener(() => chatMessagesBus.Send($"/{ChatCommandsUtils.COMMAND_GOTO} 0,0", ORIGIN));
            viewInstance.SideMenuCanvasGroup.alpha = 0;
            viewInstance.SideMenuCanvasGroup.gameObject.SetActive(false);
            new SideMenuController(viewInstance.sideMenuView);
            sceneRestrictionsController = new SceneRestrictionsController(viewInstance.sceneRestrictionsView);
            SetWorldMode(realmData.ScenesAreFixed);
            realmNavigator.RealmChanged += OnRealmChanged;
            mapPathEventBus.OnShowPinInMinimapEdge += ShowPinInMinimapEdge;
            mapPathEventBus.OnHidePinInMinimapEdge += HidePinInMinimapEdge;
            mapPathEventBus.OnRemovedDestination += HidePinInMinimapEdge;
            mapPathEventBus.OnUpdatePinPositionInMinimapEdge += UpdatePinPositionInMinimapEdge;
            viewInstance.destinationPinMarker.HidePin();
        }

        private void ExpandMinimap()
        {
            viewInstance!.collapseMinimapButton.gameObject.SetActive(true);
            viewInstance.expandMinimapButton.gameObject.SetActive(false);
            viewInstance.minimapRendererButton.gameObject.SetActive(true);
            viewInstance.minimapAnimator.SetTrigger(UIAnimationHashes.EXPAND);
        }

        private void CollapseMinimap()
        {
            viewInstance!.collapseMinimapButton.gameObject.SetActive(false);
            viewInstance.expandMinimapButton.gameObject.SetActive(true);
            viewInstance.minimapRendererButton.gameObject.SetActive(false);
            viewInstance.minimapAnimator.SetTrigger(UIAnimationHashes.COLLAPSE);
        }

        private void OpenSideMenu()
        {
            if (viewInstance!.SideMenuCanvasGroup.gameObject.activeInHierarchy) { viewInstance.SideMenuCanvasGroup.DOFade(0, ANIMATION_TIME).SetEase(Ease.InOutQuad).OnComplete(() => viewInstance.SideMenuCanvasGroup.gameObject.gameObject.SetActive(false)); }
            else
            {
                viewInstance.SideMenuCanvasGroup.gameObject.gameObject.SetActive(true);
                viewInstance.SideMenuCanvasGroup.DOFade(1, ANIMATION_TIME).SetEase(Ease.InOutQuad);
            }
        }

        private void ShowPinInMinimapEdge(IPinMarker pinMarker)
        {
            if (string.IsNullOrEmpty(pinMarker.Description)) { viewInstance!.destinationPinMarker.SetupAsScenePin(); }
            else { viewInstance!.destinationPinMarker.SetupAsMapPin(pinMarker.CurrentSprite); }
        }

        private void UpdatePinPositionInMinimapEdge(Vector2 newPosition)
        {
            viewInstance!.destinationPinMarker.RestorePin();
            viewInstance.destinationPinMarker.SetPosition(newPosition);
        }

        private void HidePinInMinimapEdge()
        {
            viewInstance!.destinationPinMarker.HidePin();
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
                    viewInstance!.pixelPerfectMapRendererTextureProvider.GetPixelPerfectTextureResolution(),
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
            mapCameraController?.SuspendRendering();
        }

        protected override void OnFocus()
        {
            mapCameraController?.ResumeRendering();

            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, true);
        }

        private void GetPlaceInfoAsync(Vector3 playerPosition)
        {
            Vector2Int playerParcelPosition = playerPosition.ToParcel();

            if (previousParcelPosition == playerParcelPosition)
                return;

            previousParcelPosition = playerParcelPosition;
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            RetrieveParcelInfoAsync(playerParcelPosition).Forget();

            bool isNotEmptyParcel = scenesCache.Contains(playerParcelPosition);
            bool isSdk7Scene = scenesCache.TryGetByParcel(playerParcelPosition, out _);
            viewInstance!.sdk6Label.gameObject.SetActive(isNotEmptyParcel && !isSdk7Scene);

            return;

            async UniTaskVoid RetrieveParcelInfoAsync(Vector2Int playerParcelPosition)
            {
                await realmData.WaitConfiguredAsync();

                try
                {
                    if (realmData.ScenesAreFixed)
                        viewInstance!.placeNameText.text = realmData.RealmName.Replace(".dcl.eth", string.Empty);
                    else
                    {
                        PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(playerParcelPosition, cts.Token);
                        viewInstance!.placeNameText.text = placeInfo?.title ?? "Unknown place";
                    }
                }
                catch (NotAPlaceException notAPlaceException)
                {
                    viewInstance!.placeNameText.text = "Unknown place";
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Not a place requested: {notAPlaceException.Message}");
                }
                catch (Exception) { viewInstance!.placeNameText.text = "Unknown place"; }
                finally { viewInstance!.placeCoordinatesText.text = playerParcelPosition.ToString().Replace("(", "").Replace(")", ""); }
            }
        }

        private void SetWorldMode(bool isWorldModeActivated)
        {
            foreach (GameObject go in viewInstance!.objectsToActivateForGenesis)
                go.SetActive(!isWorldModeActivated);

            foreach (GameObject go in viewInstance.objectsToActivateForWorlds)
                go.SetActive(isWorldModeActivated);

            viewInstance.minimapAnimator.runtimeAnimatorController = isWorldModeActivated ? viewInstance.worldsAnimatorController : viewInstance.genesisCityAnimatorController;
        }

        public override void Dispose()
        {
            cts.SafeCancelAndDispose();
            realmNavigator.RealmChanged -= OnRealmChanged;
            mapPathEventBus.OnShowPinInMinimapEdge -= ShowPinInMinimapEdge;
            mapPathEventBus.OnHidePinInMinimapEdge -= HidePinInMinimapEdge;
            sceneRestrictionsController.Dispose();
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
