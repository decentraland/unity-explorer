using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.Global;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.Profiles.Helpers;
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
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private GenericContextMenuController? genericContextMenuController;

        public GenericContextMenuPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileDataProvider)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.profileRepositoryWrapper = profileDataProvider;
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

            ControlsContainerView controlsContainerPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ControlsContainerPrefab, ct)).GetComponent<ControlsContainerView>();
            GenericContextMenuSeparatorView separatorPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuSeparatorPrefab, ct)).GetComponent<GenericContextMenuSeparatorView>();
            GenericContextMenuButtonWithTextView buttonPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuButtonPrefab, ct)).GetComponent<GenericContextMenuButtonWithTextView>();
            GenericContextMenuToggleView togglePrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuTogglePrefab, ct)).GetComponent<GenericContextMenuToggleView>();
            GenericContextMenuToggleWithIconView toggleWithIconPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuToggleWithIconPrefab, ct)).GetComponent<GenericContextMenuToggleWithIconView>();
            GenericContextMenuUserProfileView userProfilePrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuUserProfilePrefab, ct)).GetComponent<GenericContextMenuUserProfileView>();
            GenericContextMenuButtonWithStringDelegateView buttonWithStringDelegatePrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuButtonWithStringDelegatePrefab, ct)).GetComponent<GenericContextMenuButtonWithStringDelegateView>();
            GenericContextMenuTextView textPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuTextPrefab, ct)).GetComponent<GenericContextMenuTextView>();
            GenericContextMenuToggleWithCheckView toggleWithCheckPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuToggleWithCheckPrefab, ct)).GetComponent<GenericContextMenuToggleWithCheckView>();
            GenericContextMenuSubMenuButtonView subMenuButtonPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuSubMenuButtonPrefab, ct)).GetComponent<GenericContextMenuSubMenuButtonView>();
            GenericContextMenuSimpleButtonView simpleButtonPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuSimpleButtonPrefab, ct)).GetComponent<GenericContextMenuSimpleButtonView>();
            GenericContextMenuScrollableButtonListView buttonListPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.GenericContextMenuButtonListPrefab, ct)).GetComponent<GenericContextMenuScrollableButtonListView>();

            genericContextMenuController = new GenericContextMenuController(viewFactoryMethod,
                new ControlsPoolManager(profileRepositoryWrapper, panelView.ControlsContainer.transform,
                    controlsContainerPrefab,
                    separatorPrefab, buttonPrefab, togglePrefab, toggleWithIconPrefab, userProfilePrefab, buttonWithStringDelegatePrefab, textPrefab, toggleWithCheckPrefab,
                    subMenuButtonPrefab, simpleButtonPrefab, buttonListPrefab));
            mvcManager.RegisterController(genericContextMenuController);
        }

        public class GenericContextMenuSettings : IDCLPluginSettings
        {
            [field: Header(nameof(GenericContextMenuPlugin) + "." + nameof(GenericContextMenuSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuPrefab;

            [field: SerializeField]
            public AssetReferenceGameObject ControlsContainerPrefab;

            [field: Header("Controls prefabs")]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuSeparatorPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuButtonPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuTogglePrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuUserProfilePrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuButtonWithStringDelegatePrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuToggleWithIconPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuTextPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuToggleWithCheckPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuSubMenuButtonPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuSimpleButtonPrefab;
            [field: SerializeField]
            public AssetReferenceGameObject GenericContextMenuButtonListPrefab;
        }
    }
}
