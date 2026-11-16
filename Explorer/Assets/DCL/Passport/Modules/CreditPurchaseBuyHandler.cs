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
        public const string FALLBACK_FEATURE_DISABLED = "feature_disabled";
        public const string FALLBACK_RESOLUTION_FAILED = "resolution_failed";
        public const string FALLBACK_NO_CREDITS_LISTING = "no_credits_listing";

        private const int CONTRACT_ADDRESS_LENGTH = 42;

        private readonly IMVCManager mvcManager;
        private readonly MarketplaceShopAPIClient shopApiClient;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly Action stopEmotePreview;
        private readonly bool isEnabled;
        private readonly Dictionary<string, ShopListingDto?> listingCache = new ();

        public event Action<string, string, string>? FellBackToWeb;

        public CreditPurchaseBuyHandler(IMVCManager mvcManager, MarketplaceShopAPIClient shopApiClient, UnityAppWebBrowser webBrowser, Action stopEmotePreview, bool isEnabled)
        {
            this.mvcManager = mvcManager;
            this.shopApiClient = shopApiClient;
            this.webBrowser = webBrowser;
            this.stopEmotePreview = stopEmotePreview;
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

            ReadOnlySpan<char> item = urnSpan.Slice(lastColonIndex + 1);
            urnSpan = urnSpan.Slice(0, lastColonIndex);
            int secondLastColonIndex = urnSpan.LastIndexOf(':');
            ReadOnlySpan<char> contract = urnSpan.Slice(secondLastColonIndex + 1);

            // Both end up as query parameter values, so nothing but an address and a number may pass.
            if (!IsContractAddress(contract) || !IsDecimalNumber(item) || !ulong.TryParse(item, out ulong _))
                return false;

            contractAddress = contract.ToString();
            itemId = item.ToString();
            return true;
        }

        private static bool IsContractAddress(ReadOnlySpan<char> value)
        {
            if (value.Length != CONTRACT_ADDRESS_LENGTH || value[0] != '0' || value[1] != 'x')
                return false;

            for (var i = 2; i < value.Length; i++)
                if (!Uri.IsHexDigit(value[i]))
                    return false;

            return true;
        }

        private static bool IsDecimalNumber(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return false;

            foreach (char c in value)
                if (c is < '0' or > '9')
                    return false;

            return true;
        }

        public void ClearCache() =>
            listingCache.Clear();

        public async UniTask HandleBuyClickAsync(string itemUrn, string marketplaceUrl, Sprite? thumbnail, string source, Action<bool> setResolving, CancellationToken ct)
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
                thumbnail,
                marketplaceUrl,
                source,
                itemUrn);

            stopEmotePreview();

            try { await mvcManager.ShowAsync(CreditPurchaseModalController.IssueCommand(modalParams), ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        public async UniTask HandleBuyClickAsync(ShopListingDto listing, string itemUrn, string marketplaceUrl, Sprite? thumbnail, string source, CancellationToken ct)
        {
            if (!isEnabled)
            {
                FellBackToWeb?.Invoke(FALLBACK_FEATURE_DISABLED, itemUrn, source);
                webBrowser.OpenUrlMainThreadOnly(marketplaceUrl);
                return;
            }

            var modalParams = new CreditPurchaseModalControllerParams(
                listing,
                thumbnail,
                marketplaceUrl,
                source,
                itemUrn);

            stopEmotePreview();

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
