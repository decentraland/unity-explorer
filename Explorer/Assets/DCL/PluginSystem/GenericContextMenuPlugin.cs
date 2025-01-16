using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.Global;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem
{
    public class GenericContextMenuPlugin : IDCLGlobalPlugin<GenericContextMenuPlugin.GenericContextMenuSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;

        private GenericContextMenuController? genericContextMenuController;

        public GenericContextMenuPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
        }

        public void Dispose()
        {
            genericContextMenuController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // No need to inject anything into the world
        }

        public async UniTask InitializeAsync(GenericContextMenuSettings settings, CancellationToken ct)
        {
            GenericContextMenuView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuPrefab, ct: ct)).GetComponent<GenericContextMenuView>();
            ControllerBase<GenericContextMenuView, GenericContextMenuParameter>.ViewFactoryMethod viewFactoryMethod = GenericContextMenuController.Preallocate(panelViewAsset, null, out GenericContextMenuView panelView);

            GenericContextMenuSeparatorView separatorPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuSeparatorPrefab, ct)).GetComponent<GenericContextMenuSeparatorView>();
            GenericContextMenuButtonWithTextView buttonPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuButtonPrefab, ct)).GetComponent<GenericContextMenuButtonWithTextView>();
            GenericContextMenuToggleView togglePrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuTogglePrefab, ct)).GetComponent<GenericContextMenuToggleView>();

            genericContextMenuController = new GenericContextMenuController(viewFactoryMethod,
                new ControlsPoolManager(panelView.ControlsContainer, separatorPrefab, buttonPrefab, togglePrefab));
            mvcManager.RegisterController(genericContextMenuController);
        }

        public class GenericContextMenuSettings : IDCLPluginSettings
        {
            [field: Header(nameof(GenericContextMenuPlugin) + "." + nameof(GenericContextMenuSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuPrefab;

            [field: Header("Controls prefabs")]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuSeparatorPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuButtonPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuTogglePrefab;
        }
    }
}
