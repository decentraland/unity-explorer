using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterPreview;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using System;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class CharacterPreviewPlugin : IDCLGlobalPlugin<CharacterPreviewPlugin.CharacterPreviewSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly CacheCleaner cacheCleaner;

        public CharacterPreviewPlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            componentPoolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(CharacterPreviewSettings settings, CancellationToken ct)
        {
            await CreateCharacterPreviewPoolsAsync(settings, ct);
        }

        private async UniTask CreateCharacterPreviewPoolsAsync(CharacterPreviewSettings settings, CancellationToken ct)
        {
            CharacterPreviewAvatarContainer characterPreviewAvatarContainer = (await assetsProvisioner.ProvideMainAssetAsync(settings.CharacterPreviewContainerReference, ct: ct)).Value;
            var gameObjectPool = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(characterPreviewAvatarContainer));
            cacheCleaner.Register(gameObjectPool);
        }

        public class CharacterPreviewSettings : IDCLPluginSettings
        {
            [field: Header(nameof(CharacterPreviewPlugin) + "." + nameof(CharacterPreviewSettings))]
            [field: Space]
            [field: SerializeField]
            public CharacterPreviewContainerReference CharacterPreviewContainerReference;
        }

        [Serializable]
        public class CharacterPreviewContainerReference : ComponentReference<CharacterPreviewAvatarContainer>
        {
            public CharacterPreviewContainerReference(string guid) : base(guid) { }
        }
    }
}
