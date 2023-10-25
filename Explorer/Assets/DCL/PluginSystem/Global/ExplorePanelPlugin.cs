using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ExplorePanelPlugin : IDCLGlobalPlugin<ExplorePanelPlugin.ExplorePanelSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        public ExplorePanelPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
        }

        public async UniTask InitializeAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            ExplorePanelView explorePanelPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.explorePanelPrefab, ct: ct)).Value.GetComponent<ExplorePanelView>();
            mvcManager.RegisterController(new ExplorePanelController(ExplorePanelController.CreateLazily(explorePanelPrefab, null)));
            PersistentExploreOpenerView exploreOpenerPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.persistentExploreOpenerPrefab, ct: ct)).Value.GetComponent<PersistentExploreOpenerView>();
            mvcManager.RegisterController(new PersistentExploreOpenerController(PersistentExploreOpenerController.CreateLazily(exploreOpenerPrefab, null), mvcManager));
            mvcManager.Show(PersistentExploreOpenerController.IssueCommand(new MVCCheetSheet.ExampleParam("TEST"))).Forget();
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }


        public class ExplorePanelSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ExplorePanelPlugin) + "." + nameof(ExplorePanelSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject explorePanelPrefab;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject persistentExploreOpenerPrefab;
        }
    }
}
