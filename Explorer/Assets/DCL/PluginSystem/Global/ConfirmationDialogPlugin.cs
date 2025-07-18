using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.UI.ConfirmationDialog;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ConfirmationDialogPlugin: IDCLGlobalPlugin<ConfirmationDialogPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private ConfirmationDialogController? confirmationDialogController;

        public ConfirmationDialogPlugin(IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
        }

        public void Dispose()
        {
            confirmationDialogController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(ConfirmationDialogPluginSettings settings, CancellationToken ct)
        {
            ConfirmationDialogView eventInfoViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ConfirmationDialogPrefab, ct: ct)).GetComponent<ConfirmationDialogView>();
            var eventInfoViewFactory = ConfirmationDialogController.CreateLazily(eventInfoViewAsset, null);
            confirmationDialogController = new ConfirmationDialogController(eventInfoViewFactory,
                profileRepositoryWrapper);
            mvcManager.RegisterController(confirmationDialogController);
        }
    }

    public class ConfirmationDialogPluginSettings : IDCLPluginSettings
    {
        [field: Header("Confirmation dialog")]
        [field: SerializeField] internal AssetReferenceGameObject ConfirmationDialogPrefab { get; private set; }
    }
}
