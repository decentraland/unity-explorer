using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Instantiates a GameObject containing a MonoBehaviour that runs the spring bones simulation.
    /// </summary>
    public class SpringBonesPlugin : IDCLGlobalPlugin<SpringBonesSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        public SpringBonesPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(SpringBonesSettings settings, CancellationToken ct)
        {
            var simulationPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.SpringBonesSimulationPrefab, ct: ct)).Value;
            await Object.InstantiateAsync(simulationPrefab);
        }
    }

    [Serializable]
    public class SpringBonesSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceGameObject SpringBonesSimulationPrefab { get; private set; } = null!;
    }
}
