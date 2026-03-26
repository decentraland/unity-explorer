using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.Pools;
using DCL.SpringBones;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UniVRM10.FastSpringBones;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class SpringBonesPlugin : IDCLGlobalPlugin<SpringBonesPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private FastSpringBoneService springBoneService;
        private GameObjectPool<Transform> transformPool;

        public SpringBonesPlugin(IAssetsProvisioner assetsProvisioner, IComponentPoolsRegistry poolsRegistry)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.poolsRegistry = poolsRegistry;
        }

        public void Dispose()
        {
            transformPool?.Dispose();
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            var springBoneServicePrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.SpringBonesSimulationPrefab, ct)).Value;
            springBoneService = (await Object.InstantiateAsync(springBoneServicePrefab))[0];
            transformPool = new GameObjectPool<Transform>(poolsRegistry.RootContainerTransform());
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var pendingCloneRelease = new List<Transform>();
            SpringBonesSimulationSystem.InjectToWorld(ref builder, springBoneService, transformPool, pendingCloneRelease);
            SpringBoneRegistrationSystem.InjectToWorld(ref builder, springBoneService, transformPool, pendingCloneRelease);
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
