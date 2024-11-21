using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.ScreencaptureCamera;
using DCL.InWorldCamera.ScreencaptureCamera.Settings;
using DCL.InWorldCamera.ScreencaptureCamera.Systems;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using DCL.WebRequests;
using ECS;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;
using static DCL.PluginSystem.Global.InWorldCameraPlugin;
using Object = UnityEngine.Object;

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
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private ScreenRecorder recorder;
        private GameObject hud;
        private CharacterController followTarget;
        private ScreenshotMetadataBuilder metadataBuilder;
        private ICameraReelStorageService cameraReelStorageService;
        private InWorldCameraSettings settings;

        public InWorldCameraPlugin(DCLInput input, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.input = input;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            this.characterObject = characterObject;
            this.coroutineRunner = coroutineRunner;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;

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

            cameraReelStorageService = new CameraReelRemoteStorageService(new CameraReelImagesMetadataRemoteDatabase(webRequestController, decentralandUrlsSource));
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
        }
    }
}
