using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.JumpIndicator;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.PluginSystem.Global
{
    public class JumpIndicatorPlugin : IDCLGlobalPlugin<JumpIndicatorPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private DecalProjector jumpIndicatorPrefab;

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
            public JumpIndicatorPrefabReference JumpIndicatorPrefab { get; private set; }

            [Serializable]
            public class JumpIndicatorPrefabReference : ComponentReference<DecalProjector>
            {
                public JumpIndicatorPrefabReference(string guid) : base(guid) { }
            }
        }
    }
}
