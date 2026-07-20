using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class CreditPurchasePlugin : IDCLGlobalPlugin<CreditPurchasePlugin.CreditPurchaseSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ICreditsPurchaseService creditsPurchaseService;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly UnityAppWebBrowser webBrowser;

        private CreditPurchaseModalController? creditPurchaseModalController;

        public CreditPurchasePlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ICreditsPurchaseService creditsPurchaseService,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            IWeb3IdentityCache web3IdentityCache,
            UnityAppWebBrowser webBrowser)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.creditsPurchaseService = creditsPurchaseService;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.web3IdentityCache = web3IdentityCache;
            this.webBrowser = webBrowser;
        }

        public void Dispose()
        {
            creditPurchaseModalController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(CreditPurchaseSettings settings, CancellationToken ct)
        {
            CreditPurchaseModalView viewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CreditPurchasePopupPrefab, ct: ct)).GetComponent<CreditPurchaseModalView>();

            creditPurchaseModalController = new CreditPurchaseModalController(
                CreditPurchaseModalController.CreateLazily(viewAsset, null),
                creditsPurchaseService,
                marketplaceCreditsAPIClient,
                web3IdentityCache,
                webBrowser,
                OpenGetCreditsPanelAsync);

            mvcManager.RegisterController(creditPurchaseModalController);
        }

        private UniTask OpenGetCreditsPanelAsync(CancellationToken ct) =>
            mvcManager.ShowAsync(MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false)), ct);

        [Serializable]
        public class CreditPurchaseSettings : IDCLPluginSettings
        {
            [field: Header("Credit purchase")]
            [field: SerializeField] internal AssetReferenceGameObject CreditPurchasePopupPrefab { get; private set; } = null!;
        }
    }
}
