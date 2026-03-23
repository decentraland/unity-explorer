using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.SpringBones;
using System;
using System.Threading;
using UnityEngine;
using UniVRM10.FastSpringBones;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class SpringBonesPlugin : IDCLGlobalPlugin<SpringBonesPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private FastSpringBoneService springBoneService;

        public SpringBonesPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            var springBoneServicePrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.SpringBonesSimulationPrefab, ct)).Value;
            springBoneService = (await Object.InstantiateAsync(springBoneServicePrefab))[0];
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SpringBonesSimulationSystem.InjectToWorld(ref builder, springBoneService);
            SpringBoneRegistrationSystem.InjectToWorld(ref builder, springBoneService);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public SpringBonesSimulationPrefabReference SpringBonesSimulationPrefab { get; private set; }

            [Serializable]
            public class SpringBonesSimulationPrefabReference : ComponentReference<FastSpringBoneService>
            {
                public SpringBonesSimulationPrefabReference(string guid) : base(guid) { }
            }
        }
    }
}
