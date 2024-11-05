using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.CharacterCamera.Settings;
using DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Systems;
using DCL.Settings.Settings;
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
        private ProvidedAsset<GameObject> hud;

        public InWorldCameraPlugin(DCLInput input, IAssetsProvisioner assetsProvisioner)
        {
            this.input = input;
            this.assetsProvisioner = assetsProvisioner;
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
            InWorldCameraInputSystem.InjectToWorld(ref builder, input.InWorldCamera, hud.Value);
        }

        [Serializable]
        public class InWorldCameraSettings : IDCLPluginSettings
        {
            [field: Header(nameof(InWorldCameraSettings))]
            [field: SerializeField] internal AssetReferenceGameObject ScreencaptureHud { get; private set; }
        }
    }
}
