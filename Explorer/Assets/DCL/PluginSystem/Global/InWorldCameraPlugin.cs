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
using DCL.Chat;
using DCL.Chat.MessageBus;
using DCL.Clipboard;
using DCL.Input;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.PhotoDetail;
using DCL.InWorldCamera.Settings;
using DCL.InWorldCamera.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.WebRequests;
using ECS;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using Utility;
using static DCL.PluginSystem.Global.InWorldCameraPlugin;
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
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearablesProvider wearablesProvider;
        private readonly Arch.Core.World world;
        private readonly URLDomain assetBundleURL;
        private readonly ICursor cursor;
        private readonly Button sidebarButton;

        private ScreenRecorder recorder;
        private GameObject hud;
        private ScreenshotMetadataBuilder metadataBuilder;
        private InWorldCameraSettings settings;
        private InWorldCameraController inWorldCameraController;
        private CharacterController followTarget;

        public InWorldCameraPlugin(DCLInput input, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            ICameraReelStorageService cameraReelStorageService, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, IMVCManager mvcManager,
            ISystemClipboard systemClipboard, IDecentralandUrlsSource decentralandUrlsSource, IWebBrowser webBrowser, IWebRequestController webRequestController,
            IProfileRepository profileRepository, IChatMessagesBus chatMessagesBus, IAssetsProvisioner assetsProvisioner,
            IWearableStorage wearableStorage, IWearablesProvider wearablesProvider,
            Arch.Core.World world,
            URLDomain assetBundleURL,
            ICursor cursor,
            Button sidebarButton)
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
            this.chatMessagesBus = chatMessagesBus;
            this.assetsProvisioner = assetsProvisioner;
            this.wearableStorage = wearableStorage;
            this.wearablesProvider = wearablesProvider;
            this.world = world;
            this.assetBundleURL = assetBundleURL;
            this.cursor = cursor;
            this.sidebarButton = sidebarButton;

            factory = new InWorldCameraFactory();
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

            recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
            metadataBuilder = new ScreenshotMetadataBuilder(selfProfile, characterObject.Controller, realmData, placesAPIService);

            PhotoDetailView photoDetailViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.PhotoDetailPrefab, ct: ct)).GetComponent<PhotoDetailView>();
            ControllerBase<PhotoDetailView, PhotoDetailParameter>.ViewFactoryMethod viewFactoryMethod = PhotoDetailController.Preallocate(photoDetailViewAsset, null, out PhotoDetailView explorePanelView);

            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, ChatEntryConfigurationSO chatEntryConfiguration) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(settings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(settings.ChatEntryConfiguration, ct));

            mvcManager.RegisterController(new PhotoDetailController(viewFactoryMethod,
                new PhotoDetailInfoController(explorePanelView.GetComponentInChildren<PhotoDetailInfoView>(),
                    cameraReelStorageService,
                    webRequestController,
                    profileRepository,
                    mvcManager,
                    webBrowser,
                    chatMessagesBus,
                    wearableStorage,
                    wearablesProvider,
                    decentralandUrlsSource,
                    new ECSThumbnailProvider(realmData, world, assetBundleURL, webRequestController),
                    rarityBackgroundsMapping,
                    rarityColorMappings,
                    categoryIconsMapping,
                    chatEntryConfiguration),
                cameraReelScreenshotsStorage,
                systemClipboard,
                decentralandUrlsSource,
                webBrowser,
                settings.ShareToXMessage));


            inWorldCameraController = new InWorldCameraController(() => hud.GetComponent<InWorldCameraView>(), sidebarButton, world, mvcManager, cameraReelStorageService);
            mvcManager.RegisterController(inWorldCameraController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ToggleInWorldCameraActivitySystem.InjectToWorld(ref builder, settings.TransitionSettings, inWorldCameraController, followTarget, cursor, mvcManager);
            EmitInWorldCameraInputSystem.InjectToWorld(ref builder, input.InWorldCamera, input.Shortcuts.ToggleInWorldCamera);
            MoveInWorldCameraSystem.InjectToWorld(ref builder, settings.MovementSettings, characterObject.Controller.transform, cursor);
            CaptureScreenshotSystem.InjectToWorld(ref builder, recorder, playerEntity, metadataBuilder, coroutineRunner, cameraReelStorageService, inWorldCameraController);
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
            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; private set; }

            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; private set; }

            [field: SerializeField] internal AssetReferenceT<NFTColorsSO> RarityColorMappings { get; private set; }
            [field: SerializeField] internal AssetReferenceT<ChatEntryConfigurationSO> ChatEntryConfiguration { get; private set; }
        }
    }
}
