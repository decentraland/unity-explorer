using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Collections.Generic;
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

        private static async UniTask<GoalsOfTheWeekResponse> MockGoalsOfTheWeekAsync(CancellationToken ct)
        {
            await UniTask.Delay(3000, cancellationToken: ct);

            GoalsOfTheWeekResponse goalsOfTheWeekResponse = new GoalsOfTheWeekResponse
            {
                data = new GoalsOfTheWeekData
                {
                    endOfTheWeekDate = "2025-03-16T12:00:00Z",
                    totalCredits = 3.2f,
                    goals = new List<GoalData>
                    {
                        new()
                        {
                            thumbnail = "https://picsum.photos/100/100",
                            title = "Jump Into Decentraland On 3 Separate Days (Min. 10 min)",
                            progress = new GoalProgressData
                            {
                                totalSteps = 3,
                                stepsDone = 0,
                            },
                            credits = 4,
                            isClaimed = false,
                        },
                        new()
                        {
                            thumbnail = "https://picsum.photos/100/100",
                            title = "Attend 2 Events (Min. 10 min)",
                            progress = new GoalProgressData
                            {
                                totalSteps = 2,
                                stepsDone = 1,
                            },
                            credits = 2,
                            isClaimed = false,
                        },
                        new()
                        {
                            thumbnail = "https://picsum.photos/100/100",
                            title = "View 3 New Profiles",
                            progress = new GoalProgressData
                            {
                                totalSteps = 3,
                                stepsDone = 1,
                            },
                            credits = 1,
                            isClaimed = false,
                        },
                        new()
                        {
                            thumbnail = "https://picsum.photos/100/100",
                            title = "Visit 3 New Parcels",
                            progress = new GoalProgressData
                            {
                                totalSteps = 3,
                                stepsDone = 3,
                            },
                            credits = 1,
                            isClaimed = true,
                        },
                    },
                    creditsAvailableToClaim = false,
                },
            };

            return goalsOfTheWeekResponse;
        }
    }
}
