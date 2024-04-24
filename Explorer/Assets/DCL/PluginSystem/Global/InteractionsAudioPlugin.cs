using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Audio.Systems;
using DCL.Landscape;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class InteractionsAudioPlugin : IDCLWorldPlugin<InteractionsAudioPlugin.PluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private ProvidedAsset<InteractionsAudioConfigs> interactionsAudioConfigs;

        public InteractionsAudioPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InteractionsAudioSystem.InjectToWorld(ref builder, interactionsAudioConfigs.Value);
        }

        public async UniTask InitializeAsync(PluginSettings settings, CancellationToken ct)
        {
            interactionsAudioConfigs = await assetsProvisioner.ProvideMainAssetAsync(settings.InteractionsAudioConfigsReference, ct: ct);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

        public class PluginSettings : IDCLPluginSettings
        {
            [field: SerializeField] public InteractionsAudioConfigsReference InteractionsAudioConfigsReference { get; private set; }
        }

        [Serializable]
        public class InteractionsAudioConfigsReference : AssetReferenceT<InteractionsAudioConfigs>
        {
            public InteractionsAudioConfigsReference(string guid) : base(guid) { }
        }
    }
}
