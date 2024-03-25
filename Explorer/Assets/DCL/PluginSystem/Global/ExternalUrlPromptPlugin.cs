using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.ExternalUrlPrompt;
using DCL.Input;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ExternalUrlPromptPlugin : IDCLGlobalPlugin<ExternalUrlPromptPlugin.ExternalUrlPromptSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private ExternalUrlPromptController externalUrlPromptController;

        public ExternalUrlPromptPlugin(
            IAssetsProvisioner assetsProvisioner,
            IWebBrowser webBrowser,
            IMVCManager mvcManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.webBrowser = webBrowser;
            this.mvcManager = mvcManager;
        }

        public async UniTask InitializeAsync(ExternalUrlPromptSettings promptSettings, CancellationToken ct)
        {
            externalUrlPromptController = new ExternalUrlPromptController(
                ExternalUrlPromptController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(promptSettings.ExternalUrlPromptPrefab, ct: ct)).Value.GetComponent<ExternalUrlPromptView>(), null),
                webBrowser,
                new DCLCursor());

            mvcManager.RegisterController(externalUrlPromptController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose()
        {
            externalUrlPromptController.Dispose();
        }

        public class ExternalUrlPromptSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ExternalUrlPromptPlugin) + "." + nameof(ExternalUrlPromptSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject ExternalUrlPromptPrefab;
        }
    }
}
