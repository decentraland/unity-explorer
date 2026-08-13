using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
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

            // Lets an SDK7 scene raise this same confirmation through ~system/RestrictedActions. Only the
            // item URN crosses the boundary; price, signature and UI stay here.
            SceneItemPurchaseBridge.Register(this);
        }

        /// <summary>
        ///     Resolves an item URN offered by a scene into a listing and runs the standard confirmation.
        ///     The verdict is read from the modal's own events (the ones analytics already consumes) and is
        ///     deliberately coarse: a failure never tells the scene whether the wallet was short.
        /// </summary>
        public async UniTask<SceneItemPurchaseResult> OpenAsync(string itemUrn, CancellationToken ct)
        {
            if (creditPurchaseModalController == null
                || !FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsWearablePurchase)
                || !FeaturesRegistry.Instance.IsEnabled(FeatureId.UserCredits)
                || !CreditsFeatureAccess.Instance.IsUserAllowed())
                return SceneItemPurchaseResult.RejectedFeatureDisabled;

            if (!CreditPurchaseBuyHandler.TryParseCollectionItem(itemUrn, out string contractAddress, out string itemId))
                return SceneItemPurchaseResult.RejectedNotPurchasable;

            // Everything below runs on the main thread. This flow is driven from the scene runtime's
            // thread, and every piece it touches is main-thread only: the web request controller reads a
            // PersistentSetting, LoadTextureAsync creates an entity in the global ECS world, and the modal
            // is Unity UI. The passport reaches all of this from a UI callback, so it never had to switch.
            await UniTask.SwitchToMainThread(ct);

            ShopListingDto? listing;

            try { listing = await marketplaceShopAPIClient.GetShopListingForItemAsync(contractAddress, itemId, ct); }
            catch (OperationCanceledException) { return SceneItemPurchaseResult.Dismissed; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Scene offered {itemUrn} but the listing could not be resolved: {e.Message}");
                return SceneItemPurchaseResult.RejectedNotPurchasable;
            }

            if (listing == null)
                return SceneItemPurchaseResult.RejectedNotPurchasable;

            // Best-effort thumbnail: an image must never block a purchase, so a failure here just leaves
            // the card without art.
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
            catch (OperationCanceledException) { return SceneItemPurchaseResult.Dismissed; }
            catch (Exception e) { ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Thumbnail for {itemUrn} could not be loaded: {e.Message}"); }

            // The modal is a single instance shared with the passport, and MVCManager.ShowAsync returns
            // silently when its controller is not hidden. Without this check a busy modal would look
            // exactly like the player dismissing the offer.
            if (creditPurchaseModalController.State != ControllerState.ViewHidden)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Scene offered {itemUrn} while the purchase modal was already showing");
                return SceneItemPurchaseResult.Failed;
            }

            // Closing the modal without buying is the common case, so that is the default verdict.
            var verdict = SceneItemPurchaseResult.Dismissed;

            // The modal instance is shared with the passport, so an event may belong to somebody else's
            // purchase. It always carries the listing it was opened with, and that is the one we passed in.
            void OnCompleted(ShopListingDto dto, CreditsPurchaseQuote _, string __, float ___) => SetVerdict(dto, SceneItemPurchaseResult.Purchased);
            void OnFailed(ShopListingDto dto, string _, string __, string ___) => SetVerdict(dto, SceneItemPurchaseResult.Failed);
            void OnCancelled(ShopListingDto dto, string _) => SetVerdict(dto, SceneItemPurchaseResult.Dismissed);

            void SetVerdict(ShopListingDto dto, SceneItemPurchaseResult result)
            {
                if (!ReferenceEquals(dto, listing))
                    return;

                // Closing the modal after a FAILED purchase also raises PurchaseCancelled (the modal only
                // suppresses it when the purchase succeeded), so the first terminal outcome is the real one.
                // Without this, a purchase that broke reports back as if the player had declined it.
                if (verdict is SceneItemPurchaseResult.Purchased or SceneItemPurchaseResult.Failed)
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
                    // The rarity frame and the category icon come from ScriptableObject mappings that have to
                    // be assigned per plugin in the Inspector; the modal skips them when null.
                    rarityBackground: null,
                    Color.white,
                    categoryIcon: null,
                    $"{decentralandUrlsSource.Url(DecentralandUrl.MarketplaceLink)}/contracts/{contractAddress}/items/{itemId}",
                    CreditPurchaseModalControllerParams.SOURCE_SDK_SCENE);

                await mvcManager.ShowAsync(CreditPurchaseModalController.IssueCommand(modalParams), ct);
            }
            catch (OperationCanceledException) { return SceneItemPurchaseResult.Dismissed; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return SceneItemPurchaseResult.Failed;
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
        }
    }
}
