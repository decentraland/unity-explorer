using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;

namespace DCL.MarketplaceCreditsAPIService
{
    public class MarketplaceCreditsAPIClient
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string marketplaceCreditsBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.Badges);

        public MarketplaceCreditsAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<GoalsOfTheWeekResponse> FetchGoalsOfTheWeekAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/goals";

            //GoalsOfTheWeekResponse goalsOfTheWeekResponse = await webRequestController.GetAsync(url, ct, reportData: ReportCategory.MARKETPLACE_CREDITS)
            //                                                                          .CreateFromJson<GoalsOfTheWeekResponse>(WRJsonParser.Newtonsoft);
            GoalsOfTheWeekResponse goalsOfTheWeekResponse = await MockGoalsOfTheWeekAsync(ct);

            return goalsOfTheWeekResponse;
        }

        private async UniTask<GoalsOfTheWeekResponse> MockGoalsOfTheWeekAsync(CancellationToken ct)
        {
            await UniTask.Delay(3000, cancellationToken: ct);

            GoalsOfTheWeekResponse goalsOfTheWeekResponse = new GoalsOfTheWeekResponse
            {
                data = new GoalsOfTheWeekData
                {
                    endOfTheWeekDate = "2025-03-09T12:00:00Z",
                    totalCredits = 2.35948f,
                    creditsAvailableToClaim = true,
                },
            };

            return goalsOfTheWeekResponse;
        }
    }
}
