using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits;
using DCL.Profiles;
using DCL.UI.Credits;
using DCL.Web3.Identities;
using System;
using System.Threading;
using Utility;

namespace DCL.Credits
{
    public class CreditsPanelController : ICreditsPanelController
    {
        private readonly CreditsPanelView view;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Action openTopUpPanel;

        private CancellationTokenSource? loadCreditsCts;

        public CreditsPanelController(
            CreditsPanelView view,
            MarketplaceCreditsAPIClient creditsAPIClient,
            ProfileChangesBus profileChangesBus,
            IWeb3IdentityCache identityCache,
            bool topUpEnabled,
            Action openTopUpPanel)
        {
            this.view = view;
            this.creditsAPIClient = creditsAPIClient;
            this.profileChangesBus = profileChangesBus;
            this.identityCache = identityCache;
            this.openTopUpPanel = openTopUpPanel;

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);

            creditsAPIClient.OnUserCreditsFetched += OnUserCreditsFetched;
            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityCleared;

            view.GetCreditsButton.gameObject.SetActive(topUpEnabled);

            if (topUpEnabled)
                view.GetCreditsButton.onClick.AddListener(OnGetCreditsClicked);

            if (identityCache.Identity != null)
                LoadCreditsWithRestart();
        }

        public void Dispose()
        {
            loadCreditsCts.SafeCancelAndDispose();
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
            creditsAPIClient.OnUserCreditsFetched -= OnUserCreditsFetched;
            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= OnIdentityCleared;
            view.GetCreditsButton.onClick.RemoveListener(OnGetCreditsClicked);
        }

        private void OnGetCreditsClicked() =>
            openTopUpPanel();

        private void OnProfileUpdated(Profile profile) =>
            LoadCreditsWithRestart();

        private void OnUserCreditsFetched(UserCreditsResponse userCreditsResponse) =>
            view.CurrentCredits.text = userCreditsResponse.usd.credits.ToString();

        private void OnIdentityChanged() =>
            LoadCreditsWithRestart();

        private void OnIdentityCleared() =>
            loadCreditsCts.SafeCancelAndDispose();

        private void LoadCreditsWithRestart()
        {
            loadCreditsCts = loadCreditsCts.SafeRestart();
            LoadCreditsAsync(loadCreditsCts.Token).Forget();
        }

        private async UniTaskVoid LoadCreditsAsync(CancellationToken ct)
        {
            if (identityCache.Identity == null) return;

            UserCreditsResponse userCreditsResponse = await creditsAPIClient.GetUserCreditsAsync(identityCache.Identity.Address, ct);

            if (ct.IsCancellationRequested)
            {
                view.CurrentCredits.text = "0";
                return;
            }
            view.CurrentCredits.text = userCreditsResponse.usd.credits.ToString();
        }
    }
}
