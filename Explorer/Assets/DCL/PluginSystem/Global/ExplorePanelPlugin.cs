using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ExplorePanel;
using DCL.Navmap;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static UnityEngine.Object;

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
            mvcManager.RegisterController(new ExplorePanelController(
                ExplorePanelController.Preallocate(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.ExplorePanelPrefab, ct: ct)).Value.GetComponent<ExplorePanelView>(), null, out var explorePanelView)));

            mvcManager.RegisterController(new PersistentExploreOpenerController(
                PersistentExploreOpenerController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.PersistentExploreOpenerPrefab, ct: ct)).Value.GetComponent<PersistentExploreOpenerView>(), null), mvcManager)
            );

            mvcManager.RegisterController(new MinimapController(
                MinimapController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.MinimapPrefab, ct: ct)).Value.GetComponent<MinimapView>(), null), mvcManager)
            );

            NavmapController navmapController = new NavmapController(navmapView: explorePanelView.GetComponentInChildren<NavmapView>());

            //NavmapView navmapView = Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settings.navmapPrefab, ct: ct)).Value).GetComponent<NavmapView>();
            //navmapView.Hide(CancellationToken.None).Forget();
            //var navmapPlugin = new NavmapPlugin(assetsProvisioner, mvcManager, navmapView);
            mvcManager.Show(PersistentExploreOpenerController.IssueCommand(new EmptyParameter())).Forget();
            mvcManager.Show(MinimapController.IssueCommand(new EmptyParameter())).Forget();
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
            public AssetReferenceGameObject ExplorePanelPrefab;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject PersistentExploreOpenerPrefab;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject MinimapPrefab;
        }
    }
}
