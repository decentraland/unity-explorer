using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.MarketplaceCreditsAPIService
{
    /// <summary>
    /// Temporal class to mock the data for the Marketplace Credits API while the API is not available.
    /// </summary>
    public static class MarketplaceCreditsMockedData
    {
        private const bool IS_SEASON_ENDED = false;
        private const bool ARE_ALL_WEEKLY_GOALS_CLAIMED = false;

        internal static async UniTask<CreditsProgramProgressResponse> MockCreditsProgramProgressAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            CreditsProgramProgressResponse programRegistration = new CreditsProgramProgressResponse
            {
                season = new SeasonData
                {
                    startDate = "2025-03-01T00:00:00Z",
                    endDate = "2025-05-31T23:59:59Z",
                    timeLeft = IS_SEASON_ENDED ? 0 : 601200000,
                    isOutOfFunds = false,
                },
                currentWeek = new CurrentWeekData
                {
                    timeLeft = 601200000,
                },
                user = new UserData
                {
                    email = "",
                    isEmailConfirmed = false,
                },
                credits = new CreditsData
                {
                    available = 8.5f,
                    expireIn = 2630016000,
                },
                goals = new List<GoalData>
                {
                    new ()
                    {
                        title = "Jump Into Decentraland On 3 Separate Days (Min. 10 min)",
                        thumbnail = "https://picsum.photos/100/100",
                        progress = new GoalProgressData
                        {
                            totalSteps = 3,
                            completedSteps = 1,
                        },
                        reward = 4,
                        isClaimed = ARE_ALL_WEEKLY_GOALS_CLAIMED,
                    },
                    new ()
                    {
                        title = "Attend 2 Events (Min. 10 min)",
                        thumbnail = "https://picsum.photos/100/100",
                        progress = new GoalProgressData
                        {
                            totalSteps = 2,
                            completedSteps = 1,
                        },
                        reward = 2,
                        isClaimed = ARE_ALL_WEEKLY_GOALS_CLAIMED,
                    },
                    new ()
                    {
                        title = "View 3 New Profiles",
                        thumbnail = "https://picsum.photos/100/100",
                        progress = new GoalProgressData
                        {
                            totalSteps = 3,
                            completedSteps = 3,
                        },
                        reward = 1,
                        isClaimed = true,
                    },
                    new ()
                    {
                        thumbnail = "https://picsum.photos/100/100",
                        title = "Visit 3 New Parcels",
                        progress = new GoalProgressData
                        {
                            totalSteps = 3,
                            completedSteps = 3,
                        },
                        reward = 1,
                        isClaimed = ARE_ALL_WEEKLY_GOALS_CLAIMED,
                    },
                },
            };

            return programRegistration;
        }
    }
}
