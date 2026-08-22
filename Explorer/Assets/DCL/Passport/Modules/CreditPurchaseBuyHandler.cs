using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.UI;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Passport.Modules
{
    public class CreditPurchaseBuyHandler
    {
        public readonly struct ItemVisuals
        {
            public readonly string Name;
            public readonly string RarityName;
            public readonly Sprite? Thumbnail;
            public readonly Sprite? RarityBackground;
            public readonly Color RarityColor;
            public readonly Sprite? CategoryIcon;

            public ItemVisuals(string name, string rarityName, Sprite? thumbnail, Sprite? rarityBackground, Color rarityColor, Sprite? categoryIcon)
            {
                Name = name;
                RarityName = rarityName;
                Thumbnail = thumbnail;
                RarityBackground = rarityBackground;
                RarityColor = rarityColor;
                CategoryIcon = categoryIcon;
            }
        }

        public const string FALLBACK_FEATURE_DISABLED = "feature_disabled";
        public const string FALLBACK_RESOLUTION_FAILED = "resolution_failed";
        public const string FALLBACK_NO_CREDITS_LISTING = "no_credits_listing";

        private readonly IMVCManager mvcManager;
        private readonly MarketplaceShopAPIClient shopApiClient;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly bool isEnabled;
        private readonly Dictionary<string, ShopListingDto?> listingCache = new ();

        public event Action<string, string, string>? FellBackToWeb;

        public CreditPurchaseBuyHandler(IMVCManager mvcManager, MarketplaceShopAPIClient shopApiClient, UnityAppWebBrowser webBrowser, bool isEnabled)
        {
            this.mvcManager = mvcManager;
            this.shopApiClient = shopApiClient;
            this.webBrowser = webBrowser;
            this.isEnabled = isEnabled;
        }

        public static bool TryParseCollectionItem(string urn, out string contractAddress, out string itemId)
        {
            contractAddress = string.Empty;
            itemId = string.Empty;

            ReadOnlySpan<char> urnSpan = urn.AsSpan();
            int lastColonIndex = urnSpan.LastIndexOf(':');

            if (lastColonIndex == -1)
                return false;

            var item = urnSpan.Slice(lastColonIndex + 1).ToString();
            urnSpan = urnSpan.Slice(0, lastColonIndex);
            int secondLastColonIndex = urnSpan.LastIndexOf(':');
            var contract = urnSpan.Slice(secondLastColonIndex + 1).ToString();

            if (!contract.StartsWith("0x") || item.Length == 0 || !ulong.TryParse(item, out ulong _))
                return false;

            contractAddress = contract;
            itemId = item;
            return true;
        }

        public void ClearCache() =>
            listingCache.Clear();

        public async UniTask HandleBuyClickAsync(string itemUrn, string marketplaceUrl, ItemVisuals visuals, string source, Action<bool> setResolving, CancellationToken ct)
        {
            if (!isEnabled)
            {
                FellBackToWeb?.Invoke(FALLBACK_FEATURE_DISABLED, itemUrn, source);
                webBrowser.OpenUrlMainThreadOnly(marketplaceUrl);
                return;
            }

            setResolving(true);
            ShopListingDto? listing;

            try
            {
                listing = await ResolveListingAsync(itemUrn, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Listing resolution failed for {itemUrn}: {e.Message}");
                FellBackToWeb?.Invoke(FALLBACK_RESOLUTION_FAILED, itemUrn, source);
                webBrowser.OpenUrlMainThreadOnly(marketplaceUrl);
                return;
            }
            finally
            {
                setResolving(false);
            }

            if (ct.IsCancellationRequested)
                return;

            if (listing == null)
            {
                FellBackToWeb?.Invoke(FALLBACK_NO_CREDITS_LISTING, itemUrn, source);
                webBrowser.OpenUrlMainThreadOnly(marketplaceUrl);
                return;
            }

            var modalParams = new CreditPurchaseModalControllerParams(
                listing,
                visuals.Name,
                visuals.RarityName,
                visuals.Thumbnail,
                visuals.RarityBackground,
                visuals.RarityColor,
                visuals.CategoryIcon,
                marketplaceUrl,
                source,
                itemUrn);

            try { await mvcManager.ShowAsync(CreditPurchaseModalController.IssueCommand(modalParams), ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private async UniTask<ShopListingDto?> ResolveListingAsync(string itemUrn, CancellationToken ct)
        {
            if (listingCache.TryGetValue(itemUrn, out ShopListingDto? cached))
                return cached;

            if (!TryParseCollectionItem(itemUrn, out string contractAddress, out string itemId))
            {
                listingCache[itemUrn] = null;
                return null;
            }

            ShopListingDto? listing = await shopApiClient.GetShopListingForItemAsync(contractAddress, itemId, ct);
            listingCache[itemUrn] = listing;
            return listing;
        }
    }
}
