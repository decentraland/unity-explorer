using System.Threading;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Export;
using DCL.AvatarRendering.Wearables.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class VRMExportPlugin : IDCLGlobalPlugin<VRMPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWearableStorage wearableStorage;

        private VRMBonesMappingSO bonesMappingSO;

        public VRMExportPlugin(
            IAssetsProvisioner assetsProvisioner, 
            IWearableStorage wearableStorage)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.wearableStorage = wearableStorage;
        }

        public void Dispose()
        {
            bonesMappingSO.Dispose();
        }

        public async UniTask InitializeAsync(VRMPluginSettings settings, CancellationToken ct)
        {
            bonesMappingSO = (await assetsProvisioner.ProvideMainAssetAsync(settings.VRMBonesMappingAsset, ct: ct)).Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ExportAvatarSystem.InjectToWorld(ref builder, wearableStorage, bonesMappingSO);
        }
    }

    public struct VRMPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceT<VRMBonesMappingSO> VRMBonesMappingAsset { get; private set; }
    }
}
