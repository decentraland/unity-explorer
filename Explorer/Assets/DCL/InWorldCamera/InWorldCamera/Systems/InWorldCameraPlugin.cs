﻿using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.Character;
using DCL.DebugUtilities;
using DCL.Clipboard;
using DCL.Input;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.PhotoDetail;
using DCL.InWorldCamera.Settings;
using DCL.InWorldCamera.Systems;
using DCL.InWorldCamera.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Nametags;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Profiles.Self;
using DCL.Rendering.GPUInstancing;
using DCL.UI.SharedSpaceManager;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Utility;
using static DCL.PluginSystem.Global.InWorldCameraPlugin;
using Button = UnityEngine.UI.Button;
using CaptureScreenshotSystem = DCL.InWorldCamera.Systems.CaptureScreenshotSystem;
using EmitInWorldCameraInputSystem = DCL.InWorldCamera.Systems.EmitInWorldCameraInputSystem;
using MoveInWorldCameraSystem = DCL.InWorldCamera.Systems.MoveInWorldCameraSystem;
using ToggleInWorldCameraActivitySystem = DCL.InWorldCamera.Systems.ToggleInWorldCameraActivitySystem;

namespace DCL.PluginSystem.Global
{
    public class InWorldCameraPlugin : IDCLGlobalPlugin<InWorldCameraSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly SelfProfile selfProfile;
        private readonly RealmData realmData;
        private readonly Entity playerEntity;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ICharacterObject characterObject;
        private readonly ICoroutineRunner coroutineRunner;
        private readonly InWorldCameraFactory factory;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly IMVCManager mvcManager;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebBrowser webBrowser;
        private readonly IProfileRepository profileRepository;
        private readonly IRealmNavigator realmNavigator;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearablesProvider wearablesProvider;
        private readonly ICursor cursor;
        private readonly Button sidebarButton;
        private readonly Arch.Core.World globalWorld;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly NametagsData nametagsData;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly GalleryEventBus galleryEventBus;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IThumbnailProvider thumbnailProvider;

        private ScreenRecorder recorder;
        private GameObject hud;
        private ScreenshotMetadataBuilder metadataBuilder;
        private InWorldCameraSettings settings;
        private InWorldCameraController inWorldCameraController;
        private CharacterController followTarget;

        public InWorldCameraPlugin(SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService,
            ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            ICameraReelStorageService cameraReelStorageService, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, IMVCManager mvcManager,
            ISystemClipboard systemClipboard, IDecentralandUrlsSource decentralandUrlsSource, IWebBrowser webBrowser,
            IProfileRepository profileRepository,
            IRealmNavigator realmNavigator, IAssetsProvisioner assetsProvisioner,
            IWearableStorage wearableStorage, IWearablesProvider wearablesProvider,
            ICursor cursor,
            Button sidebarButton,
            Arch.Core.World globalWorld,
            IDebugContainerBuilder debugContainerBuilder,
            NametagsData nametagsData,
            ProfileRepositoryWrapper profileDataProvider,
            ISharedSpaceManager sharedSpaceManager,
            IWeb3IdentityCache web3IdentityCache,
            IThumbnailProvider thumbnailProvider,
            GalleryEventBus galleryEventBus)
        {
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            this.characterObject = characterObject;
            this.coroutineRunner = coroutineRunner;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.mvcManager = mvcManager;
            this.systemClipboard = systemClipboard;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.webBrowser = webBrowser;
            this.profileRepository = profileRepository;
            this.realmNavigator = realmNavigator;
            this.assetsProvisioner = assetsProvisioner;
            this.wearableStorage = wearableStorage;
            this.wearablesProvider = wearablesProvider;
            this.cursor = cursor;
            this.sidebarButton = sidebarButton;
            this.globalWorld = globalWorld;
            this.debugContainerBuilder = debugContainerBuilder;
            this.nametagsData = nametagsData;
            this.profileRepositoryWrapper = profileDataProvider;
            this.sharedSpaceManager = sharedSpaceManager;
            this.web3IdentityCache = web3IdentityCache;
            this.thumbnailProvider = thumbnailProvider;
            this.galleryEventBus = galleryEventBus;

            factory = new InWorldCameraFactory();
            web3IdentityCache.OnIdentityChanged += FetchCameraReelStorage;
        }

        public void Dispose()
        {
            factory.Dispose();
        }

        public async UniTask InitializeAsync(InWorldCameraSettings settings, CancellationToken ct)
        {
            this.settings = settings;

            hud = factory.CreateScreencaptureHud(await assetsProvisioner.ProvideMainAssetValueAsync(settings.ScreencaptureHud, ct: ct));
            followTarget = factory.CreateFollowTarget(await assetsProvisioner.ProvideMainAssetValueAsync(settings.FollowTarget, ct: ct));

            recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
            metadataBuilder = new ScreenshotMetadataBuilder(selfProfile, characterObject.Controller, realmData, placesAPIService);

            PhotoDetailView photoDetailViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.PhotoDetailPrefab, ct: ct)).GetComponent<PhotoDetailView>();
            ControllerBase<PhotoDetailView, PhotoDetailParameter>.ViewFactoryMethod viewFactoryMethod = PhotoDetailController.Preallocate(photoDetailViewAsset, null, out PhotoDetailView explorePanelView);

            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(settings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityBackgroundsMapping, ct))
                ;
            mvcManager.RegisterController(new PhotoDetailController(viewFactoryMethod,
                new PhotoDetailInfoController(explorePanelView.GetComponentInChildren<PhotoDetailInfoView>(),
                    cameraReelStorageService,
                    profileRepository,
                    mvcManager,
                    webBrowser,
                    realmNavigator,
                    wearableStorage,
                    wearablesProvider,
                    decentralandUrlsSource,
                    this.thumbnailProvider,
                    web3IdentityCache,
                    rarityBackgroundsMapping,
                    rarityColorMappings,
                    categoryIconsMapping,
                    profileRepositoryWrapper
                    ),
                cameraReelScreenshotsStorage,
                cameraReelStorageService,
                systemClipboard,
                decentralandUrlsSource,
                webBrowser,
                new PhotoDetailStringMessages(settings.ShareToXMessage, settings.PhotoSuccessfullyDownloadedMessage,
                    settings.PhotoSuccessfullySetAsPublicMessage, settings.LinkCopiedMessage),
                galleryEventBus));


            inWorldCameraController = new InWorldCameraController(() => hud.GetComponent<InWorldCameraView>(), sidebarButton, globalWorld, mvcManager, cameraReelStorageService, sharedSpaceManager);
            mvcManager.RegisterController(inWorldCameraController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ToggleInWorldCameraActivitySystem.InjectToWorld(ref builder, settings.TransitionSettings, inWorldCameraController, followTarget, debugContainerBuilder, cursor, DCLInput.Instance.InWorldCamera, nametagsData);
            EmitInWorldCameraInputSystem.InjectToWorld(ref builder, DCLInput.Instance.InWorldCamera);
            MoveInWorldCameraSystem.InjectToWorld(ref builder, settings.MovementSettings, characterObject.Controller.transform, cursor);
            CaptureScreenshotSystem.InjectToWorld(ref builder, recorder, playerEntity, metadataBuilder, coroutineRunner, cameraReelStorageService, inWorldCameraController);

            CleanupScreencaptureCameraSystem.InjectToWorld(ref builder);
        }

        private void FetchCameraReelStorage()
        {
            if (web3IdentityCache.Identity == null)
                return;

            cameraReelStorageService.GetUserGalleryStorageInfoAsync(web3IdentityCache.Identity.Address, CancellationToken.None).Forget();
        }

        [Serializable]
        public class InWorldCameraSettings : IDCLPluginSettings
        {
            [field: Header(nameof(InWorldCameraSettings))]
            [field: SerializeField] internal AssetReferenceGameObject ScreencaptureHud { get; private set; }
            [field: SerializeField] internal AssetReferenceGameObject FollowTarget { get; private set; }

            [field: Header("Configs")]
            [field: SerializeField] internal InWorldCameraTransitionSettings TransitionSettings { get; private set; }
            [field: SerializeField] internal InWorldCameraMovementSettings MovementSettings { get; private set; }

            [field: Header("Photo detail")]
            [field: SerializeField] internal AssetReferenceGameObject PhotoDetailPrefab { get; private set; }
            [field: SerializeField, Tooltip("Spaces will be HTTP sanitized, care for special characters")] internal string ShareToXMessage { get; private set; }
            [field: SerializeField] internal string PhotoSuccessfullyDownloadedMessage { get; private set; }
            [field: SerializeField] internal string PhotoSuccessfullySetAsPublicMessage { get; private set; }
            [field: SerializeField] internal string LinkCopiedMessage { get; private set; }
            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; private set; }

            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; private set; }

            [field: SerializeField] internal AssetReferenceT<NFTColorsSO> RarityColorMappings { get; private set; }
        }
    }
}
