using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterPreview;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class CharacterPreviewPlugin : IDCLGlobalPlugin<CharacterPreviewPlugin.CharacterPreviewSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly CacheCleaner cacheCleaner;

        private IComponentPool<CharacterPreviewContainer> characterPreviewPoolRegistry;

        public CharacterPreviewPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner, CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            componentPoolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
        }


        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            cacheCleaner.Register(characterPreviewPoolRegistry);
        }

        public async UniTask InitializeAsync(CharacterPreviewSettings settings, CancellationToken ct)
        {
            await CreateCharacterPreviewPoolAsync(settings, ct);
        }

        private async UniTask CreateCharacterPreviewPoolAsync(CharacterPreviewSettings settings, CancellationToken ct)
        {
            CharacterPreviewContainer characterPreviewContainer = (await assetsProvisioner.ProvideMainAssetAsync(settings.CharacterPreviewComponentPrefab, ct: ct)).Value.GetComponent<CharacterPreviewContainer>();
            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(characterPreviewContainer, Vector3.zero, Quaternion.identity));
            characterPreviewPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<CharacterPreviewContainer>();
        }

        public class CharacterPreviewSettings : IDCLPluginSettings
        {
            [field: Header(nameof(CharacterPreviewPlugin) + "." + nameof(CharacterPreviewSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject CharacterPreviewComponentPrefab;
        }
    }
}
