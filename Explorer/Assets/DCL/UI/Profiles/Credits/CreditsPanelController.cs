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

        private CancellationTokenSource? loadCreditsCts;

        public CreditsPanelController(
            CreditsPanelView view,
            MarketplaceCreditsAPIClient creditsAPIClient,
            ProfileChangesBus profileChangesBus,
            IWeb3IdentityCache identityCache)
        {
            this.view = view;
            this.creditsAPIClient = creditsAPIClient;
            this.profileChangesBus = profileChangesBus;
            this.identityCache = identityCache;

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);

            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityCleared;

            if (identityCache.Identity != null)
                LoadCredits();
        }

        public void Dispose()
        {
            loadCreditsCts.SafeCancelAndDispose();
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= OnIdentityCleared;
        }

        private void OnProfileUpdated(Profile profile) =>
            LoadCredits();

        private void OnIdentityChanged() =>
            LoadCredits();

        private void OnIdentityCleared() =>
            loadCreditsCts.SafeCancelAndDispose();

        private void LoadCredits()
        {
            loadCreditsCts = loadCreditsCts.SafeRestart();
            LoadCreditsAsync(loadCreditsCts.Token).Forget();
        }

        private async UniTaskVoid LoadCreditsAsync(CancellationToken ct)
        {
            if (identityCache.Identity == null) return;

            UserCreditsResponse userCreditsResponse = await creditsAPIClient.GetUserCreditsAsync(identityCache.Identity.Address, ct);

            if (ct.IsCancellationRequested) return;
            view.CurrentCredits.text = userCreditsResponse.usd.credits.ToString();
        }
    }
}
