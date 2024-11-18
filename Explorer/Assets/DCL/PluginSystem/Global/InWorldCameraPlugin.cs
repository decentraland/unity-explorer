using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.ScreencaptureCamera;
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
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private ProvidedAsset<GameObject> hudPrefab;
        private ScreenRecorder recorder;
        private GameObject hud;
        private ScreenshotMetadataBuilder metadataBuilder;
        private ICameraReelStorageService cameraReelStorageService;

        public InWorldCameraPlugin(DCLInput input, IAssetsProvisioner assetsProvisioner, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.input = input;
            this.assetsProvisioner = assetsProvisioner;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            this.characterObject = characterObject;
            this.coroutineRunner = coroutineRunner;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask InitializeAsync(InWorldCameraSettings settings, CancellationToken ct)
        {
            hudPrefab = await assetsProvisioner.ProvideMainAssetAsync(settings.ScreencaptureHud, ct);

            hudPrefab.Value.SetActive(false);
            hud = Object.Instantiate(hudPrefab.Value, Vector3.zero, Quaternion.identity);

            recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
            metadataBuilder = new ScreenshotMetadataBuilder(selfProfile, characterObject.Controller, realmData, placesAPIService);
            cameraReelStorageService = new CameraReelRemoteStorageService(new CameraReelImagesMetadataRemoteDatabase(webRequestController, decentralandUrlsSource));

        }

        public void Dispose()
        {
            hudPrefab.Dispose();
            recorder.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ToggleInWorldCameraActivitySystem.InjectToWorld(ref builder, input.InWorldCamera, hud);
            CaptureScreenshotSystem.InjectToWorld(ref builder, recorder, input.InWorldCamera, hud.GetComponent<ScreenshotHudView>(), playerEntity, metadataBuilder, coroutineRunner, cameraReelStorageService);
        }

        [Serializable]
        public class InWorldCameraSettings : IDCLPluginSettings
        {
            [field: Header(nameof(InWorldCameraSettings))]
            [field: SerializeField] internal AssetReferenceGameObject ScreencaptureHud { get; private set; }
        }
    }
}
