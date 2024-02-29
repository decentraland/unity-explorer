using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ChangeRealmPrompt;
using DCL.Input;
using Global.Dynamic;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ChangeRealmPromptPlugin : IDCLGlobalPlugin<ChangeRealmPromptPlugin.ChangeRealmPromptSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IRealmController realmController;
        private ChangeRealmPromptController changeRealmPromptController;

        public ChangeRealmPromptPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IRealmController realmController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.realmController = realmController;
        }

        public async UniTask InitializeAsync(ChangeRealmPromptSettings promptSettings, CancellationToken ct)
        {
            changeRealmPromptController = new ChangeRealmPromptController(
                ChangeRealmPromptController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(promptSettings.ChangeRealmPromptPrefab, ct: ct)).Value.GetComponent<ChangeRealmPromptView>(), null),
                new DCLCursor()/*,
                realmController*/);

            mvcManager.RegisterController(changeRealmPromptController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose()
        {
            changeRealmPromptController.Dispose();
        }

        public class ChangeRealmPromptSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ChangeRealmPromptPlugin) + "." + nameof(ChangeRealmPromptSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject ChangeRealmPromptPrefab;
        }
    }
}
