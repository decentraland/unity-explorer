using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.Character;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.Chat;
using DCL.Clipboard;
using DCL.Input;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.PassportBridgeOpener;
using DCL.InWorldCamera.PhotoDetail;
using DCL.InWorldCamera.Settings;
using DCL.InWorldCamera.Systems;
using DCL.InWorldCamera.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Nametags;
using DCL.PlacesAPIService;
using DCL.Profiles;
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
        private readonly DCLInput input;
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
        private readonly IWebRequestController webRequestController;
        private readonly IProfileRepository profileRepository;
        private readonly IRealmNavigator realmNavigator;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearablesProvider wearablesProvider;
        private readonly URLDomain assetBundleURL;
        private readonly ICursor cursor;
        private readonly Button sidebarButton;
        private readonly Arch.Core.World globalWorld;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly NametagsData nametagsData;
        private readonly ViewDependencies viewDependencies;
        private readonly GPUInstancingService gpuInstancingBuffers;
        private readonly ExposedCameraData exposedCameraData;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private ScreenRecorder recorder;
        private GameObject hud;
        private ScreenshotMetadataBuilder metadataBuilder;
        private InWorldCameraSettings settings;
        private InWorldCameraController inWorldCameraController;
        private CharacterController followTarget;

        public InWorldCameraPlugin(DCLInput input, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService,
            ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            ICameraReelStorageService cameraReelStorageService, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, IMVCManager mvcManager,
            ISystemClipboard systemClipboard, IDecentralandUrlsSource decentralandUrlsSource, IWebBrowser webBrowser, IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IRealmNavigator realmNavigator, IAssetsProvisioner assetsProvisioner,
            IWearableStorage wearableStorage, IWearablesProvider wearablesProvider,
            URLDomain assetBundleURL,
            ICursor cursor,
            Button sidebarButton,
            Arch.Core.World globalWorld,
            IDebugContainerBuilder debugContainerBuilder,
            NametagsData nametagsData,
            ViewDependencies viewDependencies,
            GPUInstancingService gpuInstancingBuffers,
            ExposedCameraData exposedCameraData,
            ISharedSpaceManager sharedSpaceManager,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.input = input;
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
            this.webRequestController = webRequestController;
            this.profileRepository = profileRepository;
            this.realmNavigator = realmNavigator;
            this.assetsProvisioner = assetsProvisioner;
            this.wearableStorage = wearableStorage;
            this.wearablesProvider = wearablesProvider;
            this.assetBundleURL = assetBundleURL;
            this.cursor = cursor;
            this.sidebarButton = sidebarButton;
            this.globalWorld = globalWorld;
            this.debugContainerBuilder = debugContainerBuilder;
            this.nametagsData = nametagsData;
            this.viewDependencies = viewDependencies;
            this.gpuInstancingBuffers = gpuInstancingBuffers;
            this.exposedCameraData = exposedCameraData;
            this.sharedSpaceManager = sharedSpaceManager;
            this.web3IdentityCache = web3IdentityCache;

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

            hud = factory.CreateScreencaptureHud(settings.ScreencaptureHud);
            followTarget = factory.CreateFollowTarget(settings.FollowTarget);

            recorder = new ScreenRecorder(hud.GetComponent<RectTransform>(), gpuInstancingBuffers);
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
                    new ECSThumbnailProvider(realmData, globalWorld, assetBundleURL, webRequestController),
                    new PassportBridgeOpener(),
                    rarityBackgroundsMapping,
                    rarityColorMappings,
                    categoryIconsMapping,
                    viewDependencies
                    ),
                cameraReelScreenshotsStorage,
                systemClipboard,
                decentralandUrlsSource,
                webBrowser,
                new PhotoDetailStringMessages(settings.ShareToXMessage, settings.PhotoSuccessfullyDownloadedMessage, settings.LinkCopiedMessage)));


            inWorldCameraController = new InWorldCameraController(() => hud.GetComponent<InWorldCameraView>(), sidebarButton, globalWorld, mvcManager, cameraReelStorageService, sharedSpaceManager);
            mvcManager.RegisterController(inWorldCameraController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ToggleInWorldCameraActivitySystem.InjectToWorld(ref builder, settings.TransitionSettings, inWorldCameraController, followTarget, debugContainerBuilder, cursor, input.InWorldCamera, nametagsData);
            EmitInWorldCameraInputSystem.InjectToWorld(ref builder, input.InWorldCamera);
            MoveInWorldCameraSystem.InjectToWorld(ref builder, settings.MovementSettings, characterObject.Controller.transform, cursor);
            CaptureScreenshotSystem.InjectToWorld(ref builder, recorder, playerEntity, metadataBuilder, coroutineRunner, cameraReelStorageService, inWorldCameraController, exposedCameraData);

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
            [field: SerializeField] internal GameObject ScreencaptureHud { get; private set; }
            [field: SerializeField] internal GameObject FollowTarget { get; private set; }

            [field: Header("Configs")]
            [field: SerializeField] internal InWorldCameraTransitionSettings TransitionSettings { get; private set; }
            [field: SerializeField] internal InWorldCameraMovementSettings MovementSettings { get; private set; }

            [field: Header("Photo detail")]
            [field: SerializeField] internal AssetReferenceGameObject PhotoDetailPrefab { get; private set; }
            [field: SerializeField, Tooltip("Spaces will be HTTP sanitized, care for special characters")] internal string ShareToXMessage { get; private set; }
            [field: SerializeField] internal string PhotoSuccessfullyDownloadedMessage { get; private set; }
            [field: SerializeField] internal string LinkCopiedMessage { get; private set; }
            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; private set; }

            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; private set; }

            [field: SerializeField] internal AssetReferenceT<NFTColorsSO> RarityColorMappings { get; private set; }
            [field: SerializeField] internal AssetReferenceT<ChatEntryConfigurationSO> ChatEntryConfiguration { get; private set; }
        }
    }
}
