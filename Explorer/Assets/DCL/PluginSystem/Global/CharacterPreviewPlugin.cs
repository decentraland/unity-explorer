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

        private IComponentPool<CharacterPreviewAvatarContainer> characterPreviewPoolRegistry;

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
            await CreateCharacterPreviewPoolAsync(settings, ct);
            cacheCleaner.Register(characterPreviewPoolRegistry);
        }

        private async UniTask CreateCharacterPreviewPoolAsync(CharacterPreviewSettings settings, CancellationToken ct)
        {
            CharacterPreviewAvatarContainer characterPreviewAvatarContainer = (await assetsProvisioner.ProvideMainAssetAsync(settings.CharacterPreviewContainerReference, ct: ct)).Value;
            var parentContainer = new GameObject("CharacterPreviewContainerPool");
            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(characterPreviewAvatarContainer), null, 1024, container => container.transform.SetParent(parentContainer.transform));
            characterPreviewPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<CharacterPreviewAvatarContainer>();
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
