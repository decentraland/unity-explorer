using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.InWorldCamera.ScreencaptureCamera;
using DCL.InWorldCamera.ScreencaptureCamera.Systems;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using ECS;
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
        private readonly InWorldCameraFactory factory;

        private ScreenRecorder recorder;
        private GameObject hud;
        private ScreenshotMetadataBuilder metadataBuilder;

        public InWorldCameraPlugin(DCLInput input, IAssetsProvisioner assetsProvisioner, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject, ICoroutineRunner coroutineRunner)
        {
            this.input = input;
            this.assetsProvisioner = assetsProvisioner;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            this.characterObject = characterObject;
            this.coroutineRunner = coroutineRunner;

            factory = new InWorldCameraFactory();
        }

        public async UniTask InitializeAsync(InWorldCameraSettings settings, CancellationToken ct)
        {
            hud = factory.CreateScreencaptureHud(await assetsProvisioner.ProvideMainAssetAsync(settings.ScreencaptureHud, ct));

            recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
            metadataBuilder = new ScreenshotMetadataBuilder(selfProfile, characterObject.Controller, realmData, placesAPIService);
        }

        public void Dispose()
        {
            factory.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ToggleInWorldCameraActivitySystem.InjectToWorld(ref builder, input.InWorldCamera, hud, factory.CreateFollowTarget());
            EmitInWorldCameraInputSystem.InjectToWorld(ref builder, input.InWorldCamera);
            MoveInWorldCameraSystem.InjectToWorld(ref builder, characterObject.Controller.transform);
            CaptureScreenshotSystem.InjectToWorld(ref builder, recorder, hud.GetComponent<ScreenshotHudView>(), playerEntity, metadataBuilder, coroutineRunner);
        }

        [Serializable]
        public class InWorldCameraSettings : IDCLPluginSettings
        {
            [field: Header(nameof(InWorldCameraSettings))]
            [field: SerializeField] internal AssetReferenceGameObject ScreencaptureHud { get; private set; }
        }
    }
}
