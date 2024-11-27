using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Character;
using DCL.Chat.MessageBus;
using DCL.Clipboard;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.PhotoDetail;
using DCL.InWorldCamera.ScreencaptureCamera;
using DCL.InWorldCamera.ScreencaptureCamera.Settings;
using DCL.InWorldCamera.ScreencaptureCamera.Systems;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
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
using UnityEngine.AddressableAssets;
using Utility;
using static DCL.PluginSystem.Global.InWorldCameraPlugin;

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
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly IMVCManager mvcManager;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebBrowser webBrowser;
        private readonly IWebRequestController webRequestController;
        private readonly IProfileRepository profileRepository;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly InWorldCameraFactory factory;

        private ScreenRecorder recorder;
        private GameObject hud;
        private CharacterController followTarget;
        private ScreenshotMetadataBuilder metadataBuilder;
        private InWorldCameraSettings settings;

        public InWorldCameraPlugin(DCLInput input, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            ICameraReelStorageService cameraReelStorageService, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, IMVCManager mvcManager,
            ISystemClipboard systemClipboard, IDecentralandUrlsSource decentralandUrlsSource, IWebBrowser webBrowser, IWebRequestController webRequestController,
            IProfileRepository profileRepository, IChatMessagesBus chatMessagesBus, IAssetsProvisioner assetsProvisioner)
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

            factory = new InWorldCameraFactory();
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

            mvcManager.RegisterController(new PhotoDetailController(viewFactoryMethod,
                new PhotoDetailInfoController(explorePanelView.GetComponentInChildren<PhotoDetailInfoView>(), cameraReelStorageService, webRequestController, profileRepository, mvcManager, webBrowser, chatMessagesBus),
                cameraReelScreenshotsStorage,
                systemClipboard,
                decentralandUrlsSource,
                webBrowser,
                settings.ShareToXMessage));
        }

        public void Dispose()
        {
            factory.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ToggleInWorldCameraActivitySystem.InjectToWorld(ref builder, settings.TransitionSettings, input.InWorldCamera, hud, followTarget);
            EmitInWorldCameraInputSystem.InjectToWorld(ref builder, input.InWorldCamera);
            MoveInWorldCameraSystem.InjectToWorld(ref builder, settings.MovementSettings, characterObject.Controller.transform);
            CaptureScreenshotSystem.InjectToWorld(ref builder, recorder, hud.GetComponent<ScreenshotHudView>(), playerEntity, metadataBuilder, coroutineRunner, cameraReelStorageService);
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
        }
    }
}
