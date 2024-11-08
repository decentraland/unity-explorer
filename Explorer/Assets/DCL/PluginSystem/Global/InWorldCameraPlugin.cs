using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Systems;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using ECS;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

        private ProvidedAsset<GameObject> hud;

        public InWorldCameraPlugin(DCLInput input, IAssetsProvisioner assetsProvisioner, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject)
        {
            this.input = input;
            this.assetsProvisioner = assetsProvisioner;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            this.characterObject = characterObject;
        }

        public async UniTask InitializeAsync(InWorldCameraSettings settings, CancellationToken ct)
        {
            hud = await assetsProvisioner.ProvideMainAssetAsync(settings.ScreencaptureHud, ct);
        }

        public void Dispose()
        {
            hud.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            EnableInWorldCameraSystem.InjectToWorld(ref builder, input.InWorldCamera, hud.Value);//, selfProfile, realmData, playerEntity, placesAPIService, characterObject);
        }

        [Serializable]
        public class InWorldCameraSettings : IDCLPluginSettings
        {
            [field: Header(nameof(InWorldCameraSettings))]
            [field: SerializeField] internal AssetReferenceGameObject ScreencaptureHud { get; private set; }
        }
    }
}
