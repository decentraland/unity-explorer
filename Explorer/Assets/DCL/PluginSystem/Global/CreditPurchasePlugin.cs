using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Browser;
using DCL.CrdtEcsBridge.JsModulesImplementation;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.TopUp;
using DCL.MarketplaceCredits.Purchase.TopUp.UI;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using DCL.UI;
using DCL.Web3.Identities;
using Decentraland.Kernel.Apis;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class CreditPurchasePlugin : IDCLGlobalPlugin<CreditPurchasePlugin.CreditPurchaseSettings>, ISceneItemPurchaseFlow
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ICreditsPurchaseService creditsPurchaseService;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly ImageControllerProvider imageControllerProvider;
        private readonly MarketplaceShopAPIClient marketplaceShopAPIClient;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private CreditPurchaseModalController? creditPurchaseModalController;
        private ICreditsTopUpService? creditsTopUpService;
        private CreditsTopUpModalController? creditsTopUpModalController;
        private NFTColorsSO rarityColorMappings;
        private NftTypeIconSO categoryIconsMapping;
        private NftTypeIconSO rarityBackgroundsMapping;

        /// <summary>
        ///     Thumbnails stay referenced until dispose, keyed by url: releasing one as soon as its modal closes
        ///     drives the texture's reference count negative when anything else still holds it. Keying by url
        ///     also bounds this by the number of DISTINCT items offered rather than growing per offer, and saves
        ///     re-downloading the art when the same machine is used twice.
        /// </summary>
        private readonly Dictionary<string, (Texture2DRef Ref, Sprite Sprite)> thumbnailsByUrl = new ();

        public CreditPurchasePlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ICreditsPurchaseService creditsPurchaseService,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            IWeb3IdentityCache web3IdentityCache,
            UnityAppWebBrowser webBrowser,
            ImageControllerProvider imageControllerProvider,
            MarketplaceShopAPIClient marketplaceShopAPIClient,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.creditsPurchaseService = creditsPurchaseService;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.web3IdentityCache = web3IdentityCache;
            this.webBrowser = webBrowser;
            this.imageControllerProvider = imageControllerProvider;
            this.marketplaceShopAPIClient = marketplaceShopAPIClient;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public void Dispose()
        {
            SceneItemPurchaseBridge.Unregister();

            foreach ((Texture2DRef textureRef, Sprite _) in thumbnailsByUrl.Values) textureRef.Dispose();
            thumbnailsByUrl.Clear();

            creditPurchaseModalController?.Dispose();
            creditsTopUpModalController?.Dispose();
            creditsTopUpService?.Dispose();
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

            CreditsTopUpModalView topUpViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CreditsTopUpPopupPrefab, ct: ct)).GetComponent<CreditsTopUpModalView>();

            creditsTopUpModalController = new CreditsTopUpModalController(
                CreditsTopUpModalController.CreateLazily(topUpViewAsset, null),
                creditsTopUpService,
                marketplaceCreditsAPIClient,
                web3IdentityCache,
                imageControllerProvider);

            mvcManager.RegisterController(creditsTopUpModalController);

            (rarityColorMappings, categoryIconsMapping, rarityBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(settings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityBackgroundsMapping, ct));

            SceneItemPurchaseBridge.Register(this);
        }

        /// <summary>
        ///     Resolves an item URN offered by a scene into a listing and runs the standard confirmation.
        ///     The verdict is read from the modal's own events (the ones analytics already consumes) and is
        ///     deliberately coarse: a failure never tells the scene whether the wallet was short.
        /// </summary>
        public async UniTask<OpenItemPurchaseResult> OpenAsync(string itemUrn, CancellationToken ct)
        {
            if (creditPurchaseModalController == null
                || !FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsWearablePurchase)
                || !FeaturesRegistry.Instance.IsEnabled(FeatureId.UserCredits)
                || !CreditsFeatureAccess.Instance.IsUserAllowed())
                return OpenItemPurchaseResult.OipRejectedFeatureDisabled;

            if (!CreditPurchaseBuyHandler.TryParseCollectionItem(itemUrn, out string contractAddress, out string itemId))
                return OpenItemPurchaseResult.OipRejectedNotPurchasable;

            await UniTask.SwitchToMainThread(ct);

            ShopListingDto? listing;

            try { listing = await marketplaceShopAPIClient.GetShopListingForItemAsync(contractAddress, itemId, ct); }
            catch (OperationCanceledException) { return OpenItemPurchaseResult.OipDismissed; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Scene offered {itemUrn} but the listing could not be resolved: {e.Message}");
                return OpenItemPurchaseResult.OipRejectedNotPurchasable;
            }

            if (listing == null)
                return OpenItemPurchaseResult.OipRejectedNotPurchasable;

            Sprite? thumbnail = null;

            try
            {
                if (!string.IsNullOrEmpty(listing.thumbnail))
                {
                    if (!thumbnailsByUrl.TryGetValue(listing.thumbnail, out (Texture2DRef Ref, Sprite Sprite) cached))
                    {
                        Texture2DRef? textureRef = await imageControllerProvider.LoadTextureAsync(listing.thumbnail, ct);

                        if (textureRef.HasValue)
                        {
                            await UniTask.SwitchToMainThread(ct);

                            Texture2D texture = textureRef.Value.Texture;
                            cached = (textureRef.Value, Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f)));
                            thumbnailsByUrl[listing.thumbnail] = cached;
                        }
                    }

                    thumbnail = cached.Sprite;
                }
            }
            catch (OperationCanceledException) { return OpenItemPurchaseResult.OipDismissed; }
            catch (Exception e) { ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Thumbnail for {itemUrn} could not be loaded: {e.Message}"); }

            if (creditPurchaseModalController.State != ControllerState.ViewHidden)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Scene offered {itemUrn} while the purchase modal was already showing");
                return OpenItemPurchaseResult.OipFailed;
            }

            var verdict = OpenItemPurchaseResult.OipDismissed;

            void OnCompleted(ShopListingDto dto, CreditsPurchaseQuote _, string __, float ___) => SetVerdict(dto, OpenItemPurchaseResult.OipPurchased);
            void OnFailed(ShopListingDto dto, string _, string __, string ___) => SetVerdict(dto, OpenItemPurchaseResult.OipFailed);
            void OnCancelled(ShopListingDto dto, string _) => SetVerdict(dto, OpenItemPurchaseResult.OipDismissed);

            void SetVerdict(ShopListingDto dto, OpenItemPurchaseResult result)
            {
                if (!ReferenceEquals(dto, listing))
                    return;

                if (verdict is OpenItemPurchaseResult.OipPurchased or OpenItemPurchaseResult.OipFailed)
                    return;

                verdict = result;
            }

            creditPurchaseModalController.PurchaseCompleted += OnCompleted;
            creditPurchaseModalController.PurchaseFailed += OnFailed;
            creditPurchaseModalController.PurchaseCancelled += OnCancelled;

            try
            {
                var modalParams = new CreditPurchaseModalControllerParams(
                    listing,
                    listing.name,
                    listing.rarity,
                    thumbnail,
                    rarityBackgroundsMapping.GetTypeImage(listing.rarity),
                    rarityColorMappings.GetColor(listing.rarity),
                    categoryIcon: categoryIconsMapping.GetTypeImage(listing.category),
                    $"{decentralandUrlsSource.Url(DecentralandUrl.MarketplaceLink)}/contracts/{contractAddress}/items/{itemId}",
                    CreditPurchaseModalControllerParams.SOURCE_SDK_SCENE);

                await mvcManager.ShowAsync(CreditPurchaseModalController.IssueCommand(modalParams), ct);
            }
            catch (OperationCanceledException)
            {
                return OpenItemPurchaseResult.OipDismissed;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return OpenItemPurchaseResult.OipFailed;
            }
            finally
            {
                creditPurchaseModalController.PurchaseCompleted -= OnCompleted;
                creditPurchaseModalController.PurchaseFailed -= OnFailed;
                creditPurchaseModalController.PurchaseCancelled -= OnCancelled;
            }

            return verdict;
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
            [field: SerializeField] internal AssetReferenceT<NFTColorsSO> RarityColorMappings { get; private set; } = null!;
            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; private set; } = null!;
            [field: SerializeField] internal AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; private set; } = null!;
        }
    }
}
