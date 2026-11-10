using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.MarketplaceCredits.Purchase.Cart.UI;
using DCL.MarketplaceCredits.Purchase.Names;
using DCL.MarketplaceCredits.Purchase.Names.UI;
using DCL.MarketplaceCredits.Purchase.TopUp;
using DCL.MarketplaceCredits.Purchase.TopUp.UI;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.UpgradeGuestAccountPopup;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using DCL.WebRequests;
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
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly SelfProfile selfProfile;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly Arch.Core.World world;
        private readonly IWearableStorage wearableStorage;
        private readonly ShopCart shopCart;
        private readonly ICreditsCartCheckoutService cartCheckoutService;
        private readonly NameClaimService nameClaimService;
        private readonly IWebRequestController webRequestController;

        private CreditPurchaseModalController? creditPurchaseModalController;
        private ShopCartModalController? shopCartModalController;
        private ICreditsTopUpService? creditsTopUpService;
        private CreditsTopUpModalController? creditsTopUpModalController;
        private NameClaimModalController? nameClaimModalController;

        public CreditPurchasePlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ICreditsPurchaseService creditsPurchaseService,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            IWeb3IdentityCache web3IdentityCache,
            UnityAppWebBrowser webBrowser,
            ImageControllerProvider imageControllerProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            CharacterPreviewEventBus characterPreviewEventBus,
            SelfProfile selfProfile,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            Arch.Core.World world,
            IWearableStorage wearableStorage,
            ShopCart shopCart,
            ICreditsCartCheckoutService cartCheckoutService,
            NameClaimService nameClaimService,
            IWebRequestController webRequestController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.creditsPurchaseService = creditsPurchaseService;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.web3IdentityCache = web3IdentityCache;
            this.webBrowser = webBrowser;
            this.imageControllerProvider = imageControllerProvider;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.selfProfile = selfProfile;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.world = world;
            this.wearableStorage = wearableStorage;
            this.shopCart = shopCart;
            this.cartCheckoutService = cartCheckoutService;
            this.nameClaimService = nameClaimService;
            this.webRequestController = webRequestController;
        }

        public void Dispose()
        {
            creditPurchaseModalController?.Dispose();
            shopCartModalController?.Dispose();
            creditsTopUpModalController?.Dispose();
            creditsTopUpService?.Dispose();
            nameClaimModalController?.Dispose();
            nameClaimService.Dispose();
            cartCheckoutService.Dispose();
            shopCart.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(CreditPurchaseSettings settings, CancellationToken ct)
        {
            CreditPurchaseModalView viewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CreditPurchasePopupPrefab, ct: ct)).GetComponent<CreditPurchaseModalView>();
            NftTypeIconSO rarityInfoPanelBackgrounds = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityInfoPanelBackgroundsMapping, ct);

            creditPurchaseModalController = new CreditPurchaseModalController(
                CreditPurchaseModalController.CreateLazily(viewAsset, null),
                creditsPurchaseService,
                marketplaceCreditsAPIClient,
                web3IdentityCache,
                webBrowser,
                characterPreviewFactory,
                characterPreviewEventBus,
                selfProfile,
                profileRepositoryWrapper,
                world,
                wearableStorage,
                rarityInfoPanelBackgrounds,
                OpenGetCreditsPanelAsync,
                OpenBackpackPanelAsync);

            mvcManager.RegisterController(creditPurchaseModalController);

            creditsTopUpService = new CreditsTopUpService(marketplaceCreditsAPIClient, web3IdentityCache, webBrowser);

            CreditsTopUpModalView topUpViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CreditsTopUpPopupPrefab, ct: ct)).GetComponent<CreditsTopUpModalView>();

            creditsTopUpModalController = new CreditsTopUpModalController(
                CreditsTopUpModalController.CreateLazily(topUpViewAsset, null),
                creditsTopUpService,
                marketplaceCreditsAPIClient,
                web3IdentityCache,
                imageControllerProvider);

            mvcManager.RegisterController(creditsTopUpModalController);

            await RegisterShopCartModalAsync(settings, rarityInfoPanelBackgrounds, ct);
            await RegisterNameClaimModalAsync(settings, ct);
        }

        private async UniTask RegisterNameClaimModalAsync(CreditPurchaseSettings settings, CancellationToken ct)
        {
            if (!settings.NameClaimPopupPrefab.RuntimeKeyIsValid())
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, "NameClaimPopupPrefab is not assigned in the plugin settings: the NAME claim modal is disabled.");
                return;
            }

            NameClaimModalView nameClaimViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.NameClaimPopupPrefab, ct: ct)).GetComponent<NameClaimModalView>();

            nameClaimModalController = new NameClaimModalController(
                NameClaimModalController.CreateLazily(nameClaimViewAsset, null),
                nameClaimService,
                marketplaceCreditsAPIClient,
                web3IdentityCache,
                OpenGetCreditsPanelAsync);

            mvcManager.RegisterController(nameClaimModalController);
        }

        private async UniTask RegisterShopCartModalAsync(CreditPurchaseSettings settings, NftTypeIconSO rarityInfoPanelBackgrounds, CancellationToken ct)
        {
            if (!settings.ShopCartPopupPrefab.RuntimeKeyIsValid())
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, "ShopCartPopupPrefab is not assigned in the plugin settings: the shop cart modal is disabled.");
                return;
            }

            ShopCartModalView cartViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ShopCartPopupPrefab, ct: ct)).GetComponent<ShopCartModalView>();

            shopCartModalController = new ShopCartModalController(
                ShopCartModalController.CreateLazily(cartViewAsset, null),
                shopCart,
                cartCheckoutService,
                marketplaceCreditsAPIClient,
                web3IdentityCache,
                new SpriteCache(webRequestController),
                rarityInfoPanelBackgrounds,
                OpenGetCreditsFromCartAsync,
                OpenBackpackPanelAsync);

            mvcManager.RegisterController(shopCartModalController);
        }

        private UniTask OpenGetCreditsFromCartAsync(CancellationToken ct)
        {
            if (web3IdentityCache.IsGuest())
                return mvcManager.ShowAsync(UpgradeGuestAccountPopupController.IssueCommand(new UpgradeGuestAccountPopupController.Params(GuestUpgradeTrigger.Credits)), ct);

            return FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsTopup) && CreditsFeatureAccess.Instance.IsUserAllowed()
                ? mvcManager.ShowAsync(CreditsTopUpModalController.IssueCommand(new CreditsTopUpModalControllerParams(CreditsTopUpModalControllerParams.SOURCE_CART_MODAL)), ct)
                : mvcManager.ShowAsync(MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false)), ct);
        }

        private UniTask OpenGetCreditsPanelAsync(CancellationToken ct)
        {
            if (web3IdentityCache.IsGuest())
                return mvcManager.ShowAsync(UpgradeGuestAccountPopupController.IssueCommand(new UpgradeGuestAccountPopupController.Params(GuestUpgradeTrigger.Credits)), ct);

            return FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsTopup) && CreditsFeatureAccess.Instance.IsUserAllowed()
                ? mvcManager.ShowAsync(CreditsTopUpModalController.IssueCommand(new CreditsTopUpModalControllerParams(CreditsTopUpModalControllerParams.SOURCE_PURCHASE_MODAL)), ct)
                : mvcManager.ShowAsync(MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false)), ct);
        }

        private UniTask OpenBackpackPanelAsync(CancellationToken ct) =>
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Avatar)), ct);

        [Serializable]
        public class CreditPurchaseSettings : IDCLPluginSettings
        {
            [field: Header("Credit purchase")]
            [field: SerializeField] public AssetReferenceGameObject CreditPurchasePopupPrefab { get; private set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject CreditsTopUpPopupPrefab { get; private set; } = null!;
            [field: SerializeField] public AssetReferenceT<NftTypeIconSO> RarityInfoPanelBackgroundsMapping { get; private set; } = null!;

            [field: Header("Shop cart")]
            [field: SerializeField] public AssetReferenceGameObject ShopCartPopupPrefab { get; private set; } = null!;

            [field: Header("Shop names")]
            [field: SerializeField] public AssetReferenceGameObject NameClaimPopupPrefab { get; private set; } = null!;
        }
    }
}
