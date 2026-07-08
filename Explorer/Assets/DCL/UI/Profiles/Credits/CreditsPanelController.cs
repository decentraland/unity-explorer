using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits;
using DCL.Profiles;
using DCL.UI.Credits;
using System;
using System.Threading;

namespace DCL.Credits
{
    public class CreditsPanelController : IDisposable
    {
        private readonly CreditsPanelView view;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly ProfileChangesBus profileChangesBus;

        public CreditsPanelController(
            CreditsPanelView view,
            MarketplaceCreditsAPIClient creditsAPIClient,
            ProfileChangesBus profileChangesBus)
        {
            this.view = view;
            this.creditsAPIClient = creditsAPIClient;
            this.profileChangesBus = profileChangesBus;

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);
        }

        private void OnProfileUpdated(Profile profile)
        {
            GetCredits(profile.UserId, default).Forget();

            async UniTaskVoid GetCredits(string userId, CancellationToken cs)
            {
                UserCreditsResponse userCreditsResponse = await creditsAPIClient.GetUserCreditsAsync(userId, cs);
                view.CurrentCredits.text = userCreditsResponse.usd.credits.ToString();
            }
    }

        public void Dispose()
        {
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
        }
    }
}
