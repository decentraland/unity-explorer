using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.UI.ErrorPopup;
using DCL.Utilities.Extensions;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ErrorPopupPlugin : IDCLGlobalPlugin<ErrorPopupPlugin.ErrorPopupSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;

        public ErrorPopupPlugin(IMVCManager mvcManager, IAssetsProvisioner assetsProvisioner)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(ErrorPopupSettings settings, CancellationToken ct)
        {
            var reference = settings.ErrorPopup.EnsureNotNull("ErrorPopup is null in settings");
            var errorPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(reference, ct: CancellationToken.None)).Value;
            var errorPopupView = errorPopupPrefab.GetComponent<ErrorPopupView>().EnsureNotNull($"{nameof(ErrorPopupView)} not found in the asset");
            mvcManager.RegisterController(new ErrorPopupController(errorPopupView));
        }

        public class ErrorPopupSettings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceGameObject ErrorPopup { get; private set; } = null!;
        }
    }
}
