using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Names;
using DCL.MarketplaceCredits.Purchase.Names.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Shop
{
    /// <summary>
    ///     The Names section: a UNIQUE NAME tab that checks a typed name against the registrar and opens the
    ///     claim modal, and a RESALE NAME tab with the grid of the names on sale. The nametag over the avatar preview shows the name
    ///     typed or picked in the open tab, or the player's own while there is none.
    /// </summary>
    public class ShopNamesSectionController : IDisposable
    {
        private const int AVAILABILITY_DEBOUNCE_MS = 1000;
        private const string CHECKING_TEXT = "Checking availability...";
        private const string TAKEN_TEXT = "This NAME is taken. Choose another NAME or make an offer to the owner.";
        private const string TOO_SHORT_TEXT = "NAMEs need at least {0} characters.";
        private const string CLAIMED_NOTIFICATION = "{0} is now your NAME!";
        private const string NOT_REGISTERED_NOTIFICATION = "The payment went through but {0} could not be registered. Please contact support.";
        private const string NAMES_BROWSE_PATH = "/names/browse?search=";

        private readonly ShopNamesSectionView view;
        private readonly GameObject previewNametagRoot;
        private readonly SimpleUserNameElement previewNametag;
        private readonly NameClaimService claimService;
        private readonly SelfProfile selfProfile;
        private readonly IMVCManager mvcManager;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource urlsSource;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? checkCts;
        private Profile? ownProfile;
        private ShopNamesTab currentTab = ShopNamesTab.Unique;
        private string selectedResaleName = string.Empty;

        public ShopNamesResaleController Resale { get; }

        public bool IsResaleShown => currentTab == ShopNamesTab.Resale;

        public event Action<ShopNamesTab>? TabChanged;

        public ShopNamesSectionController(
            ShopNamesSectionView view,
            GameObject previewNametagRoot,
            SimpleUserNameElement previewNametag,
            NameClaimService claimService,
            MarketplaceShopAPIClient api,
            SelfProfile selfProfile,
            IMVCManager mvcManager,
            UnityAppWebBrowser webBrowser,
            IDecentralandUrlsSource urlsSource)
        {
            this.view = view;
            this.previewNametagRoot = previewNametagRoot;
            this.previewNametag = previewNametag;
            this.claimService = claimService;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            this.webBrowser = webBrowser;
            this.urlsSource = urlsSource;
            Resale = new ShopNamesResaleController(view.ResaleGrid, api, webBrowser, urlsSource);

            view.TabClicked += ShowTab;
            Resale.NameSelected += OnResaleNameSelected;
            view.NameChanged += OnNameChanged;
            view.ClaimClicked += OnClaimClicked;
            view.MakeOfferClicked += OnMakeOfferClicked;
            claimService.Completed += OnClaimCompleted;

            previewNametagRoot.SetActive(false);
        }

        public void Dispose()
        {
            view.TabClicked -= ShowTab;
            Resale.NameSelected -= OnResaleNameSelected;
            Resale.Dispose();
            view.NameChanged -= OnNameChanged;
            view.ClaimClicked -= OnClaimClicked;
            view.MakeOfferClicked -= OnMakeOfferClicked;
            claimService.Completed -= OnClaimCompleted;
            lifeCts.SafeCancelAndDispose();
            checkCts.SafeCancelAndDispose();
        }

        public void Activate()
        {
            lifeCts = lifeCts.SafeRestart();

            view.gameObject.SetActive(true);
            currentTab = ShopNamesTab.Unique;
            view.ShowTab(ShopNamesTab.Unique);
            view.ClearName();
            view.HideStatus();
            view.SetClaimEnabled(false);
            view.SetClaimPrice(string.Empty);

            LoadNametagAsync(lifeCts.Token).Forget();
            LoadPriceAsync(lifeCts.Token).Forget();
        }

        public void Deactivate()
        {
            lifeCts.SafeCancelAndDispose();
            lifeCts = null;
            checkCts.SafeCancelAndDispose();
            checkCts = null;
            previewNametagRoot.SetActive(false);
            ownProfile = null;
            Resale.Deactivate();
            selectedResaleName = string.Empty;
            currentTab = ShopNamesTab.Unique;
            view.gameObject.SetActive(false);
        }

        private void ShowTab(ShopNamesTab tab)
        {
            if (tab == currentTab)
                return;

            currentTab = tab;
            view.ShowTab(tab);

            if (tab == ShopNamesTab.Resale)
                Resale.Activate();
            else
                Resale.Deactivate();

            selectedResaleName = string.Empty;
            RefreshNametag(tab == ShopNamesTab.Resale ? selectedResaleName : view.Name);
            TabChanged?.Invoke(tab);
        }

        private void OnResaleNameSelected(string name)
        {
            selectedResaleName = name;
            RefreshNametag(name);
        }

        private async UniTaskVoid LoadNametagAsync(CancellationToken ct)
        {
            ProfileReadResult readResult = await selfProfile.ProfileAsync(ct);

            if (ct.IsCancellationRequested || !readResult.IsOk(out Profile? profile))
                return;

            ownProfile = profile;
            previewNametagRoot.SetActive(true);
            RefreshNametag(currentTab == ShopNamesTab.Resale ? selectedResaleName : view.Name);
            view.SetHash(profile.Compact.WalletId ?? string.Empty);
        }

        private void RefreshNametag(string previewedName)
        {
            if (ownProfile == null)
                return;

            if (previewedName.Length > 0)
                previewNametag.Setup(previewedName, string.Empty, hasClaimedName: true, NameColorHelper.GetNameColor(previewedName));
            else
                previewNametag.Setup(ownProfile.Compact);

            // The pill fits a nested fitter, which a single layout pass leaves one frame behind
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)previewNametagRoot.transform);
        }

        private async UniTaskVoid LoadPriceAsync(CancellationToken ct)
        {
            NameQuoteResult quote = await claimService.QuoteAsync(ct);

            if (ct.IsCancellationRequested || !quote.Success)
                return;

            // 100 MANA at the live rate, so the credits price floats.
            view.SetClaimPrice($"≈{quote.Quote.Credits}");
        }

        private void OnNameChanged(string name)
        {
            RefreshNametag(name);
            view.HideStatus();
            view.SetClaimEnabled(false);
            checkCts = checkCts.SafeRestart();

            if (name.Length > 0)
                CheckAvailabilityAsync(name, checkCts.Token).Forget();
        }

        private async UniTaskVoid CheckAvailabilityAsync(string name, CancellationToken ct)
        {
            if (await UniTask.Delay(AVAILABILITY_DEBOUNCE_MS, cancellationToken: ct).SuppressCancellationThrow())
                return;

            if (NameRules.Validate(name) != NameValidation.Valid)
            {
                view.ShowStatus(string.Format(TOO_SHORT_TEXT, NameRules.MIN_LENGTH), isError: true, offerAvailable: false);
                return;
            }

            view.ShowStatus(CHECKING_TEXT, isError: false, offerAvailable: false);

            NameAvailability availability = await claimService.CheckAvailabilityAsync(name, ct);

            if (ct.IsCancellationRequested)
                return;

            if (availability == NameAvailability.Taken)
            {
                view.ShowStatus(TAKEN_TEXT, isError: true, offerAvailable: true);
                return;
            }

            view.HideStatus();
            view.SetClaimEnabled(true);
        }

        private void OnClaimClicked()
        {
            if (lifeCts == null || NameRules.Validate(view.Name) != NameValidation.Valid)
                return;

            if (!claimService.IsAvailable)
            {
                webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.MarketplaceClaimName);
                return;
            }

            ShowClaimModalAsync(view.Name, lifeCts.Token).Forget();
        }

        private async UniTaskVoid ShowClaimModalAsync(string name, CancellationToken ct)
        {
            try { await mvcManager.ShowAsync(NameClaimModalController.IssueCommand(new NameClaimModalParams(name)), ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private void OnMakeOfferClicked() =>
            webBrowser.OpenUrlMainThreadOnly($"{urlsSource.Url(DecentralandUrl.MarketplaceLink)}{NAMES_BROWSE_PATH}{view.Name}");

        private void OnClaimCompleted(string name, NameClaimResult result)
        {
            if (result.Error == NameClaimError.NotRegistered)
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(string.Format(NOT_REGISTERED_NOTIFICATION, name)));

            if (result.Status != NameClaimStatus.Registered)
                return;

            NotificationsBusController.Instance.AddNotification(new DefaultSuccessNotification(string.Format(CLAIMED_NOTIFICATION, name)));

            if (!string.Equals(view.Name, name, StringComparison.Ordinal))
                return;

            view.ClearName();
            RefreshNametag(string.Empty);
            view.HideStatus();
            view.SetClaimEnabled(false);
        }
    }
}
