using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Input;
using DCL.NftInfoAPIService;
using DCL.NftPrompt;
using DCL.WebRequests;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class NftPromptPlugin : IDCLGlobalPlugin<NftPromptPlugin.NftPromptSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly INftInfoAPIService nftInfoAPIService;
        private readonly IWebRequestController webRequestController;
        private NftPromptController nftPromptController;

        public NftPromptPlugin(
            IAssetsProvisioner assetsProvisioner,
            IWebBrowser webBrowser,
            IMVCManager mvcManager,
            INftInfoAPIService nftInfoAPIService,
            IWebRequestController webRequestController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.webBrowser = webBrowser;
            this.mvcManager = mvcManager;
            this.nftInfoAPIService = nftInfoAPIService;
            this.webRequestController = webRequestController;
        }

        public async UniTask InitializeAsync(NftPromptSettings promptSettings, CancellationToken ct)
        {
            nftPromptController = new NftPromptController(
                NftPromptController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(promptSettings.NftPromptPrefab, ct: ct)).Value.GetComponent<NftPromptView>(), null),
                webBrowser,
                new DCLCursor(),
                nftInfoAPIService,
                webRequestController);

            mvcManager.RegisterController(nftPromptController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose()
        {
            nftPromptController.Dispose();
        }

        public class NftPromptSettings : IDCLPluginSettings
        {
            [field: Header(nameof(NftPromptPlugin) + "." + nameof(NftPromptSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject NftPromptPrefab;
        }
    }
}
