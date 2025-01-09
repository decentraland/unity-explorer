using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Audio.Systems;
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
    public class PointerInputAudioPlugin : IDCLWorldPlugin<PointerInputAudioPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private ProvidedAsset<PointerInputAudioConfigs> interactionsAudioConfigs;

        public PointerInputAudioPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            PointerInputAudioSystem.InjectToWorld(ref builder, interactionsAudioConfigs.Value);
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            interactionsAudioConfigs = await assetsProvisioner.ProvideMainAssetAsync(settings.InteractionsAudioConfigsReference, ct: ct);
        }

        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public InteractionsAudioConfigsReference InteractionsAudioConfigsReference { get; private set; }
        }

        [Serializable]
        public class InteractionsAudioConfigsReference : AssetReferenceT<PointerInputAudioConfigs>
        {
            public InteractionsAudioConfigsReference(string guid) : base(guid) { }
        }
    }
}
