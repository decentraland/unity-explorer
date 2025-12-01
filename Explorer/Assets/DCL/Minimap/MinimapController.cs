using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.ExplorePanel;
using DCL.MapRenderer;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.UI;
using DCL.UI.SharedSpaceManager;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;
using DCL.UI.Controls.Configs;
using DG.Tweening;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Utility;

namespace DCL.Minimap
{
    public partial class MinimapController : ControllerBase<MinimapView>, IMapActivityOwner
    {
        private const MapLayer RENDER_LAYERS = MapLayer.SatelliteAtlas | MapLayer.PlayerMarker | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers | MapLayer.Pins | MapLayer.Path | MapLayer.LiveEvents | MapLayer.HomeMarker;
        private const string DEFAULT_BACK_FROM_WORLD_TEXT = "JUMP BACK TO GENESIS CITY";
        private const string RELOAD_SCENE_TEXT = "RELOAD SCENE";
        private static readonly Dictionary<string, string> CUSTOM_BACK_FROM_WORLD_TEXTS = new ()
        {
            { "onboardingdcl.dcl.eth", "EXIT TUTORIAL" }
        };
        private const float ANIMATION_TIME = 0.2f;
        private const int SHOW_BANNED_TOOLTIP_DELAY_SEC = 10;

        private readonly IMapRenderer mapRenderer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IRealmNavigator realmNavigator;
        private readonly IScenesCache scenesCache;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly Vector2Int startParcelInGenesis;
        private readonly CancellationTokenSource disposeCts;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrls;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ReloadSceneChatCommand reloadSceneCommand;
        private readonly IRoomHub roomHub;
        private readonly ILoadingStatus loadingStatus;
        private readonly bool includeBannedUsersFromScene;
        private readonly HomePlaceEventBus homePlaceEventBus;

        private GenericContextMenu? contextMenu;
        private CancellationTokenSource? placesApiCts;
        private CancellationTokenSource? favoriteCancellationToken = new ();
        private MapRendererTrackPlayerPosition mapRendererTrackPlayerPosition;
        private IMapCameraController? mapCameraController;
        private Vector2Int previousParcelPosition;
        private SceneRestrictionsController? sceneRestrictionsController;
        private bool isOwnPlayerBanned;
        private ToggleContextMenuControlSettings homeToggleSettings;
        private CancellationTokenSource showBannedTooltipCts;

        public IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; } = new Dictionary<MapLayer, IMapLayerParameter>
            { { MapLayer.PlayerMarker, new PlayerMarkerParameter { BackgroundIsActive = false } } };

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public MinimapController(
            MinimapView minimapView,
            IMapRenderer mapRenderer,
            IMVCManager mvcManager,
            IPlacesAPIService placesAPIService,
            IRealmData realmData,
            IRealmNavigator realmNavigator,
            IScenesCache scenesCache,
            IMapPathEventBus mapPathEventBus,
            ISceneRestrictionBusController sceneRestrictionBusController,
            Vector2Int startParcelInGenesis,
            ISharedSpaceManager sharedSpaceManager,
            ISystemClipboard systemClipboard,
            IDecentralandUrlsSource decentralandUrls,
            IChatMessagesBus chatMessagesBus,
            ReloadSceneChatCommand reloadSceneCommand,
            IRoomHub roomHub,
            ILoadingStatus loadingStatus,
            bool includeBannedUsersFromScene,
            HomePlaceEventBus homePlaceEventBus) 
                : base(() => minimapView)
        {
            this.mapRenderer = mapRenderer;
            this.mvcManager = mvcManager;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.realmNavigator = realmNavigator;
            this.scenesCache = scenesCache;
            this.mapPathEventBus = mapPathEventBus;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.startParcelInGenesis = startParcelInGenesis;
            this.sharedSpaceManager = sharedSpaceManager;
            this.systemClipboard = systemClipboard;
            this.decentralandUrls = decentralandUrls;
            this.chatMessagesBus = chatMessagesBus;
            this.reloadSceneCommand = reloadSceneCommand;
            this.roomHub = roomHub;
            this.loadingStatus = loadingStatus;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
            this.homePlaceEventBus = homePlaceEventBus;
            minimapView.SetCanvasActive(false);
            disposeCts = new CancellationTokenSource();
        }

        public override void Dispose()
        {
            placesApiCts.SafeCancelAndDispose();
            disposeCts.Cancel();
            mapPathEventBus.OnShowPinInMinimapEdge -= ShowPinInMinimapEdge;
            mapPathEventBus.OnHidePinInMinimapEdge -= HidePinInMinimapEdge;

            if (includeBannedUsersFromScene)
            {
                roomHub.SceneRoom().CurrentSceneRoomForbiddenAccess -= ShowOwnPlayerBannedMark;
                roomHub.SceneRoom().CurrentSceneRoomConnected -= HideOwnPlayerBannedMark;
                roomHub.SceneRoom().CurrentSceneRoomDisconnected -= HideOwnPlayerBannedMark;
                showBannedTooltipCts.SafeCancelAndDispose();
            }

            sceneRestrictionsController?.Dispose();
            viewInstance?.minimapContextualButtonView.Button.onClick.RemoveAllListeners();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public void HookPlayerPositionTrackingSystem(TrackPlayerPositionSystem system) =>
            AddModule(new BridgeSystemBinding<TrackPlayerPositionSystem>(this, QueryPlayerPositionQuery, system));

        private void OnRealmChanged(RealmKind realmKind)
        {
            SetGenesisMode(realmKind is RealmKind.GenesisCity);
            previousParcelPosition = new Vector2Int(int.MaxValue, int.MaxValue);
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.expandMinimapButton.onClick.AddListener(ExpandMinimap);
            viewInstance.collapseMinimapButton.onClick.AddListener(CollapseMinimap);
            viewInstance.minimapRendererButton.Button.onClick.AddListener(() => sharedSpaceManager.ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Navmap)));
            viewInstance.sideMenuButton.onClick.AddListener(OpenSideMenu);
            viewInstance.favoriteButton.OnButtonClicked += OnFavoriteButtonClicked;

            viewInstance.SideMenuCanvasGroup.alpha = 0;
            viewInstance.SideMenuCanvasGroup.gameObject.SetActive(false);
            new SideMenuController(viewInstance.sideMenuView);
            sceneRestrictionsController = new SceneRestrictionsController(viewInstance.sceneRestrictionsView, sceneRestrictionBusController);
            SetGenesisMode(realmData.IsGenesis());
            realmData.RealmType.OnUpdate += OnRealmChanged;
            mapPathEventBus.OnShowPinInMinimapEdge += ShowPinInMinimapEdge;
            mapPathEventBus.OnHidePinInMinimapEdge += HidePinInMinimapEdge;
            mapPathEventBus.OnRemovedDestination += HidePinInMinimapEdge;
            mapPathEventBus.OnUpdatePinPositionInMinimapEdge += UpdatePinPositionInMinimapEdge;

            viewInstance.destinationPinMarker.HidePin();
            viewInstance.sdk6Label.gameObject.SetActive(false);
            
            contextMenu = new GenericContextMenu()
                          // Add title control to prevent incorrect layout height when the context menu has a single control
                          // May be removed if a new control is added
                         .AddControl(new TextContextMenuControlSettings("Scene's Options"))
                         .AddControl(new SeparatorContextMenuControlSettings())
                         .AddControl(homeToggleSettings = new ToggleContextMenuControlSettings("Set as Home", SetAsHomeToggledAsync))
                         .AddControl(new SeparatorContextMenuControlSettings())
                         .AddControl(new ButtonContextMenuControlSettings("Copy Link", viewInstance.contextMenuConfig.copyLinkIcon, CopyJumpInLink));

            SetInitialHomeToggleValue();
            viewInstance.contextMenuConfig.button.onClick.AddListener(ShowContextMenu);

            if (includeBannedUsersFromScene)
            {
                roomHub.SceneRoom().CurrentSceneRoomForbiddenAccess += ShowOwnPlayerBannedMark;
                roomHub.SceneRoom().CurrentSceneRoomConnected += HideOwnPlayerBannedMark;
                roomHub.SceneRoom().CurrentSceneRoomDisconnected += HideOwnPlayerBannedMark;
            }
        }

        private void SetInitialHomeToggleValue()
        {
            bool isHome = homePlaceEventBus.CurrentHomeCoordinates == previousParcelPosition;
            homeToggleSettings.SetInitialValue(isHome);
        }

        private void ShowContextMenu()
        {
            SetInitialHomeToggleValue();
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                           new GenericContextMenuParameter(contextMenu, viewInstance!.contextMenuConfig.button.transform.position)))
                      .Forget();
        }

        private void SetAsHomeToggledAsync(bool value)
        {
            if (value)
                homePlaceEventBus.SetAsHome(previousParcelPosition);
            else
                homePlaceEventBus.UnsetHome();
            
            // Opening context menu loses focus of minimap, so for pin to showup immediately we have to simulate 
            // gaining focus again.
            OnFocus();
        }
        
        private void OnFavoriteButtonClicked(bool value)
        {
            // Setting button for immediate graphic change.
            viewInstance!.favoriteButton.SetButtonState(value);
            favoriteCancellationToken = favoriteCancellationToken.SafeRestart();
            SetAsFavoriteAsync(favoriteCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SetAsFavoriteAsync(CancellationToken ct)
            {
                try
                {
                    PlacesData.PlaceInfo? placeInfo = await GetPlaceInfoAsync(previousParcelPosition, favoriteCancellationToken.Token);
                    if (placeInfo == null)
                    {
                        viewInstance!.favoriteButton.SetButtonState(false, false);
                        return;
                    }
                    await placesAPIService.SetPlaceFavoriteAsync(placeInfo!.id, value, ct);
                    viewInstance!.favoriteButton.SetButtonState(value);
                }
                catch (OperationCanceledException _) { }
                catch (Exception e)
                {
                    viewInstance!.favoriteButton.SetButtonState(false);
                    ReportHub.LogError(ReportCategory.GENERIC_WEB_REQUEST, $"Failed setting place as favorite + {e}");
                }
            }
        }

        private void CopyJumpInLink()
        {
            string link = realmData.ScenesAreFixed
                ? $"{decentralandUrls.Url(DecentralandUrl.Host)}/jump?realm={realmData.RealmName}&position={previousParcelPosition.x},{previousParcelPosition.y}"
                : $"{decentralandUrls.Url(DecentralandUrl.Host)}/jump?position={previousParcelPosition.x},{previousParcelPosition.y}";

            systemClipboard.Set(link);
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

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(PBAvatarShape))]
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

                //Once the render target image is ready to be shown, we enable the minimap
                viewInstance.SetCanvasActive(true);
                UpdatePlaceDisplayAsync(position);
            }
            else
            {
                mapRendererTrackPlayerPosition.OnPlayerPositionChanged(position);
                UpdatePlaceDisplayAsync(position);
            }
        }

        protected override void OnBlur()
        {
            mapCameraController?.SuspendRendering();
            mapRenderer.SetSharedLayer(MapLayer.ScenesOfInterest, false);
        }

        protected override void OnFocus()
        {
            mapCameraController?.ResumeRendering();

            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, true);
            mapRenderer.SetSharedLayer(MapLayer.ScenesOfInterest, true);

            ForceUpdateFavoriteButton();
        }

        private void ForceUpdateFavoriteButton()
        {
            // Using same token as player position based update, because player position based update takes priority
            // and using same token prevents race condition.
            placesApiCts.SafeCancelAndDispose();
            placesApiCts = new CancellationTokenSource();
            ForceUpdateFavoriteButtonAsync().Forget();

            async UniTaskVoid ForceUpdateFavoriteButtonAsync()
            {
                try
                {
                    PlacesData.PlaceInfo? placeInfo = 
                        await GetPlaceInfoAsync(previousParcelPosition, placesApiCts.Token, true);
                    if (placeInfo == null)
                    {
                        viewInstance!.favoriteButton.SetButtonState(false, false);
                        return;
                    }
                    viewInstance!.favoriteButton.SetButtonState(placeInfo.user_favorite);
                }
                catch (NotAPlaceException _)
                {
                    viewInstance!.favoriteButton.SetButtonState(false, false);
                }
                catch (Exception _) { }
            };
        }

        private void UpdatePlaceDisplayAsync(Vector3 playerPosition)
        {
            Vector2Int playerParcelPosition = playerPosition.ToParcel();

            if (previousParcelPosition == playerParcelPosition)
                return;

            previousParcelPosition = playerParcelPosition;
            placesApiCts.SafeCancelAndDispose();
            placesApiCts = new CancellationTokenSource();
            RefreshPlaceInfoUIAsync(playerParcelPosition, placesApiCts.Token).Forget();
            
            // This is disabled until we figure out a better way to inform the user if the current is scene is SDK6 or not
            // bool isNotEmptyParcel = scenesCache.Contains(playerParcelPosition);
            // bool isSdk7Scene = scenesCache.TryGetByParcel(playerParcelPosition, out _);
            // viewInstance!.sdk6Label.gameObject.SetActive(isNotEmptyParcel && !isSdk7Scene);
        }
        
        private async UniTaskVoid RefreshPlaceInfoUIAsync(Vector2Int parcelPosition, CancellationToken ct)
        {
            PlacesData.PlaceInfo? placeInfo = await GetPlaceInfoAsync(parcelPosition, ct);

            if (realmData.ScenesAreFixed)
            {
                viewInstance!.placeNameText.text = realmData.RealmName.Replace(".dcl.eth", string.Empty);
                viewInstance!.favoriteButton.SetButtonState(false, false);
            }
            else if (placeInfo != null)
            {
                viewInstance!.placeNameText.text = placeInfo.title;
                viewInstance!.favoriteButton.SetButtonState(placeInfo.user_favorite);
            }
            else
            {
                viewInstance!.placeNameText.text = "Unknown place";
                viewInstance!.favoriteButton.SetButtonState(false, false);
            }
    
            viewInstance!.placeCoordinatesText.text = parcelPosition.ToString().Replace("(", "").Replace(")", "");
        }

        private async UniTask<PlacesData.PlaceInfo?> GetPlaceInfoAsync(Vector2Int parcelPosition, CancellationToken ct,
            bool renewCache = false)
        {
            await realmData.WaitConfiguredAsync();

            try
            {
                if (realmData.ScenesAreFixed)
                    return null;
        
                return await placesAPIService.GetPlaceAsync(parcelPosition, ct, renewCache);
            }
            catch (NotAPlaceException notAPlaceException)
            {
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Not a place requested: {notAPlaceException.Message}");
                return null;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.GENERIC_WEB_REQUEST, $"Could not get place API: {e.Message}");
                return null;
            }
        }

        private void SetGenesisMode(bool isGenesisModeActivated)
        {
            if (viewInstance == null)
                return;

            ToggleObjects(isGenesisModeActivated);
            ConfigureContextualButton(isGenesisModeActivated);
            SetAnimatorController(isGenesisModeActivated);
        }

        private void ToggleObjects(bool isGenesisModeActivated)
        {
            foreach (GameObject go in viewInstance!.objectsToActivateForGenesis)
                go.SetActive(isGenesisModeActivated);

            foreach (GameObject go in viewInstance.objectsToActivateForWorlds)
                go.SetActive(!isGenesisModeActivated);
        }

        private void ConfigureContextualButton(bool isGenesisModeActivated)
        {
            // Interactivity
            viewInstance!.minimapContextualButtonView.SetInteractable(CanInteractWithContextualButton(isGenesisModeActivated));

            // Text
            string buttonText = GetContextualButtonText(isGenesisModeActivated);
            viewInstance.minimapContextualButtonView.SetText(buttonText);

            // Action
            viewInstance.minimapContextualButtonView.Button.onClick.RemoveAllListeners();
            UnityAction buttonAction = GetContextualButtonAction(isGenesisModeActivated);
            viewInstance.minimapContextualButtonView.Button.onClick.AddListener(buttonAction);
        }

        private bool CanInteractWithContextualButton(bool isGenesisModeActivated) =>
            !isGenesisModeActivated &&
            (!realmData.IsLocalSceneDevelopment ||
             (realmData.IsLocalSceneDevelopment && scenesCache.CurrentScene.Value != null));

        private string GetContextualButtonText(bool isGenesisModeActivated)
        {
            if (isGenesisModeActivated)
                return DEFAULT_BACK_FROM_WORLD_TEXT;

            if (realmData.IsLocalSceneDevelopment)
                return RELOAD_SCENE_TEXT;

            return CUSTOM_BACK_FROM_WORLD_TEXTS.GetValueOrDefault(
                realmData.RealmName, DEFAULT_BACK_FROM_WORLD_TEXT
            );
        }

        private UnityAction GetContextualButtonAction(bool isGenesisModeActivated)
        {
            if (isGenesisModeActivated || !realmData.IsLocalSceneDevelopment)
            {
                return () => realmNavigator
                            .TeleportToParcelAsync(startParcelInGenesis, disposeCts.Token, false)
                            .Forget();
            }

            return () => chatMessagesBus.SendWithUtcNowTimestamp(
                ChatChannel.NEARBY_CHANNEL, $"/{reloadSceneCommand.Command}", ChatMessageOrigin.MINIMAP
            );
        }

        private void SetAnimatorController(bool isGenesisModeActivated)
        {
            viewInstance!.minimapAnimator.runtimeAnimatorController =
                isGenesisModeActivated
                    ? viewInstance.genesisCityAnimatorController
                    : viewInstance.worldsAnimatorController;
        }

        private void ShowOwnPlayerBannedMark()
        {
            if (isOwnPlayerBanned)
                return;

            isOwnPlayerBanned = true;
            viewInstance!.ownPlayerBannedMark.SetActive(true);

            showBannedTooltipCts = showBannedTooltipCts.SafeRestart();
            ShowBannedTooltipAsync(showBannedTooltipCts.Token).Forget();
        }

        private void HideOwnPlayerBannedMark()
        {
            viewInstance!.ownPlayerBannedMark.SetActive(false);
            isOwnPlayerBanned = false;
        }

        private async UniTaskVoid ShowBannedTooltipAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);
            viewInstance!.ownPlayerBannedTooltip.SetActive(true);
            await UniTask.Delay(TimeSpan.FromSeconds(SHOW_BANNED_TOOLTIP_DELAY_SEC), cancellationToken: ct);
            viewInstance!.ownPlayerBannedTooltip.SetActive(false);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayerPositionSystem : ControllerECSBridgeSystem
    {
        internal TrackPlayerPositionSystem(World world) : base(world) { }
    }
}
