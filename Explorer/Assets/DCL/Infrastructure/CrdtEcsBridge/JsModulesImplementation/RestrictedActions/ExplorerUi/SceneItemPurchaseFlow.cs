using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using DCL.UI;
using ECS.Unity.ExplorerUiEvents;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Infrastructure.CrdtEcsBridge.JsModulesImplementation.RestrictedActions
{
    /// <summary>
    ///     Runs the credits purchase of an item a scene offered by URN: resolves the URN to its primary
    ///     listing, shows the purchase modal and reports how it ended. One instance serves every scene,
    ///     because the modal and the thumbnails it shows are client wide. Main thread only.
    /// </summary>
    public class SceneItemPurchaseFlow : IDisposable
    {
        private readonly IMVCManager mvcManager;
        private readonly MarketplaceShopAPIClient shopApiClient;
        private readonly ImageControllerProvider imageControllerProvider;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        // Kept until disposal and keyed by url: a thumbnail released when its modal closes drives the
        // reference count of a texture something else still holds below zero.
        private readonly Dictionary<string, (Texture2DRef textureRef, Sprite sprite)> thumbnails = new ();

        public virtual bool IsAvailable =>
            FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsWearablePurchase)
            && FeaturesRegistry.Instance.IsEnabled(FeatureId.UserCredits)
            && CreditsFeatureAccess.Instance.IsUserAllowed();

        public SceneItemPurchaseFlow(
            IMVCManager mvcManager,
            MarketplaceShopAPIClient shopApiClient,
            ImageControllerProvider imageControllerProvider,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.mvcManager = mvcManager;
            this.shopApiClient = shopApiClient;
            this.imageControllerProvider = imageControllerProvider;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public void Dispose()
        {
            foreach ((Texture2DRef textureRef, Sprite sprite) in thumbnails.Values)
            {
                Object.Destroy(sprite);
                textureRef.Dispose();
            }

            thumbnails.Clear();
        }

        /// <summary>
        ///     Resolves once the purchase is over, which for a shown modal is when it closes. Never throws:
        ///     an item that cannot be sold, and any error on the way, is a
        ///     <see cref="ExplorerItemPurchaseStatus.Failed" />.
        /// </summary>
        public virtual async UniTask<ExplorerItemPurchaseStatus> RunAsync(string itemUrn, CancellationToken ct)
        {
            if (!CreditPurchaseBuyHandler.TryParseCollectionItem(itemUrn, out string contractAddress, out string itemId))
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"A scene offered '{itemUrn}', which is not a collection item urn");
                return ExplorerItemPurchaseStatus.Failed;
            }

            try
            {
                ShopListingDto? listing = await shopApiClient.GetShopListingForItemAsync(contractAddress, itemId, ct);

                if (listing == null)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"A scene offered '{itemUrn}', which has no primary listing");
                    return ExplorerItemPurchaseStatus.Failed;
                }

                Sprite? thumbnail = await GetThumbnailAsync(listing.thumbnail, ct);

                // Stays null when the modal never ran, which is what MVC does with a show request for a
                // controller that is already on screen.
                CreditPurchaseModalOutcome? outcome = null;

                var modalParams = new CreditPurchaseModalControllerParams(
                    listing,
                    thumbnail,
                    $"{decentralandUrlsSource.Url(DecentralandUrl.MarketplaceLink)}/contracts/{contractAddress}/items/{itemId}",
                    CreditPurchaseModalControllerParams.SOURCE_SDK_SCENE,
                    itemUrn,
                    closedWith => outcome = closedWith);

                await mvcManager.ShowAsync(CreditPurchaseModalController.IssueCommand(modalParams), ct);

                return outcome switch
                       {
                           CreditPurchaseModalOutcome.Purchased => ExplorerItemPurchaseStatus.Purchased,
                           CreditPurchaseModalOutcome.Dismissed => ExplorerItemPurchaseStatus.Dismissed,
                           _ => ExplorerItemPurchaseStatus.Failed,
                       };
            }
            catch (OperationCanceledException) { return ExplorerItemPurchaseStatus.Dismissed; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                return ExplorerItemPurchaseStatus.Failed;
            }
        }

        private async UniTask<Sprite?> GetThumbnailAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            if (thumbnails.TryGetValue(url, out (Texture2DRef textureRef, Sprite sprite) cached))
                return cached.sprite;

            try
            {
                Texture2DRef? textureRef = await imageControllerProvider.LoadTextureAsync(url, ct);

                if (textureRef == null)
                    return null;

                Texture2D texture = textureRef.Value.Texture;
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                thumbnails[url] = (textureRef.Value, sprite);
                return sprite;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"The thumbnail of a scene offered item could not be loaded: {e.Message}");
                return null;
            }
        }
    }
}
