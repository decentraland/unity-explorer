using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Shop;
using DCL.UI;
using ECS.Unity.ExplorerUiEvents;
using MVC;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        private const string URN_CONTRACT_GROUP = "contract";
        private const string URN_ITEM_GROUP = "item";

        // The only shape a scene may offer: an item of a Polygon collection. The address and the item id
        // go into the listing query, so they are matched to the character.
        private static readonly Regex SCENE_ITEM_URN = new (
            @"\Aurn:decentraland:(?:matic|amoy):collections-v2:(?<contract>0x[0-9a-fA-F]{40}):(?<item>[0-9]+)\z",
            RegexOptions.CultureInvariant);

        private readonly IMVCManager mvcManager;
        private readonly MarketplaceShopAPIClient shopApiClient;
        private readonly ImageControllerProvider imageControllerProvider;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        // Kept until disposal and keyed by url: a thumbnail released when its modal closes drives the
        // reference count of a texture something else still holds below zero.
        private readonly Dictionary<string, (Texture2DRef textureRef, Sprite sprite)> thumbnails = new ();

        private bool reserved;

        public virtual bool IsAvailable =>
            FeaturesRegistry.Instance.IsEnabled(FeatureId.SceneItemPurchase)
            && FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsWearablePurchase)
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
        ///     Takes the one purchase slot all scenes share. False while another purchase holds it or the
        ///     modal is on screen for any other reason. A caller that got true must call
        ///     <see cref="Release" /> once its purchase is over.
        /// </summary>
        public bool TryReserve()
        {
            // reserved covers the time the item is still being resolved, when no modal is on screen yet.
            if (reserved || mvcManager.IsShowing<CreditPurchaseModalView, CreditPurchaseModalControllerParams>())
                return false;

            reserved = true;
            return true;
        }

        public void Release() =>
            reserved = false;

        /// <summary>
        ///     Resolves once the purchase is over, which for a shown modal is when it closes. Never throws:
        ///     an item that cannot be sold, and any error on the way, is a
        ///     <see cref="ExplorerItemPurchaseStatus.Failed" />.
        /// </summary>
        public virtual async UniTask<ExplorerItemPurchaseStatus> RunAsync(string itemUrn, CancellationToken ct)
        {
            try
            {
                Match urnMatch = SCENE_ITEM_URN.Match(itemUrn);

                if (!urnMatch.Success)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, "A scene offered a urn that is not a collection item");
                    return ExplorerItemPurchaseStatus.Failed;
                }

                string contractAddress = urnMatch.Groups[URN_CONTRACT_GROUP].Value;
                string itemId = urnMatch.Groups[URN_ITEM_GROUP].Value;

                ShopListingDto? listing = await shopApiClient.GetShopListingForItemAsync(contractAddress, itemId, ct);

                if (listing == null)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"A scene offered '{itemUrn}', which has no primary listing");
                    return ExplorerItemPurchaseStatus.Failed;
                }

                if (!IsListingOf(listing, contractAddress, itemId))
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"The listing resolved for '{itemUrn}' is {listing.contractAddress}:{listing.itemId} on chain {listing.chainId}, not the item the scene offered");
                    return ExplorerItemPurchaseStatus.Failed;
                }

                // Built from the listing that is charged, never from the scene's string, so the item the
                // modal previews is the item it sells.
                string listingUrn = ShopItemLinks.BuildItemUrn(listing.chainId, listing.contractAddress, itemId);

                Sprite? thumbnail = await GetThumbnailAsync(listing.thumbnail, ct);

                // Stays null when the modal never ran, which is what MVC does with a show request for a
                // controller that is already on screen.
                CreditPurchaseModalOutcome? outcome = null;

                var modalParams = new CreditPurchaseModalControllerParams(
                    listing,
                    thumbnail,
                    $"{decentralandUrlsSource.Url(DecentralandUrl.MarketplaceLink)}/contracts/{listing.contractAddress}/items/{listing.itemId}",
                    CreditPurchaseModalControllerParams.SOURCE_SDK_SCENE,
                    listingUrn,
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

        private static bool IsListingOf(ShopListingDto listing, string contractAddress, string itemId) =>
            ShopItemLinks.HasDerivableUrn(listing.chainId)
            && string.Equals(listing.contractAddress, contractAddress, StringComparison.OrdinalIgnoreCase)
            && string.Equals(listing.itemId, itemId, StringComparison.Ordinal);

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
