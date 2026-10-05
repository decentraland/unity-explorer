using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.TextShape.Fonts.Settings;
using ECS.LifeCycle;
using ECS.StreamableLoading.Fonts;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.World
{
    public class FontsLoadingPlugin : IDCLWorldPlugin<FontsLoadingPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly FontsCache fontsCache;
        private readonly bool tryUnlistedBundles;

        private ProvidedAsset<SoFontList> fontList;
        private RuntimeFontAssetFactory fontAssetFactory = null!;

        public FontsLoadingPlugin(CacheCleaner cacheCleaner, IAssetsProvisioner assetsProvisioner, bool tryUnlistedBundles)
        {
            this.tryUnlistedBundles = tryUnlistedBundles;
            this.assetsProvisioner = assetsProvisioner;

            fontsCache = new FontsCache();
            cacheCleaner.Register(fontsCache);
        }

        public void Dispose()
        {
            fontsCache.Dispose();
            fontList.Dispose();
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            fontList = await assetsProvisioner.ProvideMainAssetAsync(settings.FontList, ct);

            fontAssetFactory = new RuntimeFontAssetFactory(fontList.Value.Font(DCL.ECSComponents.Font.FSansSerif)
                                                           ?? throw new InvalidOperationException("The font list has no FSansSerif font"));
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LoadFontSystem.InjectToWorld(ref builder, fontsCache, fontAssetFactory, tryUnlistedBundles);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<SoFontList> FontList { get; private set; } = null!;
        }
    }
}
