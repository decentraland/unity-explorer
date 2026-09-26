using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Fonts;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.World
{
    public class FontsLoadingPlugin : IDCLWorldPlugin<FontsLoadingPlugin.Settings>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly FontsCache fontsCache;
        private readonly FontFileStore fontFileStore;

        private ProvidedAsset<TMP_FontAsset> referenceFont;
        private RuntimeFontAssetFactory fontAssetFactory = null!;

        public FontsLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner, IAssetsProvisioner assetsProvisioner)
        {
            this.webRequestController = webRequestController;
            this.assetsProvisioner = assetsProvisioner;

            fontsCache = new FontsCache();
            cacheCleaner.Register(fontsCache);
            fontFileStore = FontFileStore.InPersistentData();
        }

        public void Dispose()
        {
            fontsCache.Dispose();
            referenceFont.Dispose();
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            referenceFont = await assetsProvisioner.ProvideMainAssetAsync(settings.ReferenceFont, ct);
            fontAssetFactory = new RuntimeFontAssetFactory(referenceFont.Value);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LoadFontSystem.InjectToWorld(ref builder, fontsCache, webRequestController, fontAssetFactory, fontFileStore);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<TMP_FontAsset> ReferenceFont { get; private set; } = null!;
        }
    }
}
