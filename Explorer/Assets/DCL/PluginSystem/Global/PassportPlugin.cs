using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.Passport;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class PassportPlugin : IDCLGlobalPlugin<PassportPlugin.PassportSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private PassportController passportController;
        private readonly ICursor cursor;

        public PassportPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager, ICursor cursor)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.cursor = cursor;
        }

        public async UniTask InitializeAsync(PassportSettings promptSettings, CancellationToken ct)
        {
            passportController = new PassportController(
                PassportController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(promptSettings.PassportPrefab, ct: ct)).Value.GetComponent<PassportView>(), null),
                cursor);

            mvcManager.RegisterController(passportController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose() =>
            passportController.Dispose();

        public class PassportSettings : IDCLPluginSettings
        {
            [field: Header(nameof(PassportPlugin) + "." + nameof(PassportSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject PassportPrefab;
        }
    }
}
