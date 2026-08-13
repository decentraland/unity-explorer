using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.TopUp;
using DCL.MarketplaceCredits.Purchase.TopUp.UI;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.UI;
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
        private readonly ImageControllerProvider imageControllerProvider;

        private CreditPurchaseModalController? creditPurchaseModalController;
        private ICreditsTopUpService? creditsTopUpService;
        private CreditsTopUpModalController? creditsTopUpModalController;
        private UnityApplicationFocusSource? applicationFocusSource;

        public CreditPurchasePlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ICreditsPurchaseService creditsPurchaseService,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            IWeb3IdentityCache web3IdentityCache,
            UnityAppWebBrowser webBrowser,
            ImageControllerProvider imageControllerProvider)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.creditsPurchaseService = creditsPurchaseService;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.web3IdentityCache = web3IdentityCache;
            this.webBrowser = webBrowser;
            this.imageControllerProvider = imageControllerProvider;
        }

        public void Dispose()
        {
            creditPurchaseModalController?.Dispose();
            creditsTopUpModalController?.Dispose();
            creditsTopUpService?.Dispose();
            applicationFocusSource?.Dispose();
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
                OpenGetCreditsPanelAsync,
                OpenBackpackPanelAsync);

            mvcManager.RegisterController(creditPurchaseModalController);

            creditsTopUpService = new CreditsTopUpService(marketplaceCreditsAPIClient, web3IdentityCache, webBrowser);
            applicationFocusSource = new UnityApplicationFocusSource();

            CreditsTopUpModalView topUpViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CreditsTopUpPopupPrefab, ct: ct)).GetComponent<CreditsTopUpModalView>();

            creditsTopUpModalController = new CreditsTopUpModalController(
                CreditsTopUpModalController.CreateLazily(topUpViewAsset, null),
                creditsTopUpService,
                marketplaceCreditsAPIClient,
                web3IdentityCache,
                imageControllerProvider,
                applicationFocusSource);

            mvcManager.RegisterController(creditsTopUpModalController);
        }

        private UniTask OpenGetCreditsPanelAsync(CancellationToken ct) =>
            FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsTopup) && CreditsFeatureAccess.Instance.IsUserAllowed()
                ? mvcManager.ShowAsync(CreditsTopUpModalController.IssueCommand(new CreditsTopUpModalControllerParams(CreditsTopUpModalControllerParams.SOURCE_PURCHASE_MODAL)), ct)
                : mvcManager.ShowAsync(MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false)), ct);

        private UniTask OpenBackpackPanelAsync(CancellationToken ct) =>
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Avatar)), ct);

        [Serializable]
        public class CreditPurchaseSettings : IDCLPluginSettings
        {
            [field: Header("Credit purchase")]
            [field: SerializeField] internal AssetReferenceGameObject CreditPurchasePopupPrefab { get; private set; } = null!;
            [field: SerializeField] internal AssetReferenceGameObject CreditsTopUpPopupPrefab { get; private set; } = null!;
        }
    }
}
