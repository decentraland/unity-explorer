using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Character;
using DCL.Clipboard;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.PhotoDetail;
using DCL.InWorldCamera.ScreencaptureCamera;
using DCL.InWorldCamera.ScreencaptureCamera.Systems;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using ECS;
using MVC;
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
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly IMVCManager mvcManager;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebBrowser webBrowser;

        private ProvidedAsset<GameObject> hudPrefab;
        private ScreenRecorder recorder;
        private GameObject hud;
        private ScreenshotMetadataBuilder metadataBuilder;

        public InWorldCameraPlugin(DCLInput input, IAssetsProvisioner assetsProvisioner, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            ICameraReelStorageService cameraReelStorageService, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, IMVCManager mvcManager,
            ISystemClipboard systemClipboard, IDecentralandUrlsSource decentralandUrlsSource, IWebBrowser webBrowser)
        {
            this.input = input;
            this.assetsProvisioner = assetsProvisioner;
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
        }

        public async UniTask InitializeAsync(InWorldCameraSettings settings, CancellationToken ct)
        {
            hudPrefab = await assetsProvisioner.ProvideMainAssetAsync(settings.ScreencaptureHud, ct);
            PhotoDetailView photoDetailViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.PhotoDetailPrefab, ct: ct)).GetComponent<PhotoDetailView>();
            ControllerBase<PhotoDetailView, PhotoDetailParameter>.ViewFactoryMethod viewFactoryMethod = PhotoDetailController.Preallocate(photoDetailViewAsset, null, out PhotoDetailView explorePanelView);

            hudPrefab.Value.SetActive(false);
            hud = Object.Instantiate(hudPrefab.Value, Vector3.zero, Quaternion.identity);

            recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
            metadataBuilder = new ScreenshotMetadataBuilder(selfProfile, characterObject.Controller, realmData, placesAPIService);

            mvcManager.RegisterController(new PhotoDetailController(viewFactoryMethod,
                new PhotoDetailInfoController(explorePanelView.GetComponentInChildren<PhotoDetailInfoView>(), cameraReelStorageService),
                cameraReelScreenshotsStorage,
                systemClipboard,
                decentralandUrlsSource,
                webBrowser,
                settings.ShareToXMessage));
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

            [field: Header("Photo detail")]
            [field: SerializeField] internal AssetReferenceGameObject PhotoDetailPrefab { get; private set; }
            [field: SerializeField, Tooltip("Spaces will be HTTP sanitized, care for special characters")] internal string ShareToXMessage { get; private set; }
        }
    }
}
