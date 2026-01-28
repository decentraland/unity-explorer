using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.JumpIndicator;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class JumpIndicatorPlugin : IDCLGlobalPlugin<JumpIndicatorPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private GameObject jumpIndicatorPrefab;

        public JumpIndicatorPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct) =>
            jumpIndicatorPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.JumpIndicatorPrefab, ct)).Value;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            JumpIndicatorSystem.InjectToWorld(ref builder, arguments.PlayerEntity, jumpIndicatorPrefab);

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<GameObject> JumpIndicatorPrefab { get; private set; }
        }
    }
}
