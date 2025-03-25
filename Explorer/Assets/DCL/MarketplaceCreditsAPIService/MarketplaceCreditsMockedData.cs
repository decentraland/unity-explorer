using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.MarketplaceCreditsAPIService
{
    /// <summary>
    /// Temporal class to mock the data for the Marketplace Credits API while the API is not available.
    /// </summary>
    public static class MarketplaceCreditsMockedData
    {
        public static string CurrentMockedEmail = "test@test.com";
        public static bool CurrentMockedEmailConfirmed = true;

        internal static async UniTask<CreditsProgramProgressResponse> MockCreditsProgramProgressAsync(string email, bool isEmailConfirmed, CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            CreditsProgramProgressResponse programRegistration = new CreditsProgramProgressResponse
            {
                season = new SeasonData
                {
                    startDate = "2025-03-01T00:00:00Z",
                    endDate = "2025-05-31T23:59:59Z",
                    timeLeft = 601200000,
                    isOutOfFunds = false,
                },
                currentWeek = new CurrentWeekData
                {
                    timeLeft = 601200000,
                },
                user = new UserData
                {
                    email = email,
                    isEmailConfirmed = isEmailConfirmed,
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
                        isClaimed = false,
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
                        isClaimed = false,
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
                        isClaimed = false,
                    },
                },
            };

            CurrentMockedEmail = email;
            CurrentMockedEmailConfirmed = isEmailConfirmed;

            return programRegistration;
        }

        internal static async Task<CaptchaResponse> MockCaptchaAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            return new CaptchaResponse
            {
                captchaUrl = "https://i.ibb.co/RG48r508/Test-Captcha.png",
            };
        }

        internal static async UniTask<ClaimCreditsResponse> MockClaimCreditsAsync(CancellationToken ct)
        {
            float randomClaimedCredits = ((float)new System.Random().NextDouble() * 4) + 1;
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            ClaimCreditsResponse responseData = new ClaimCreditsResponse
            {
                success = true,
                claimedCredits = randomClaimedCredits,
            };

            return responseData;
        }

        internal static async UniTask MockRemoveRegistrationAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            CurrentMockedEmail = string.Empty;
            CurrentMockedEmailConfirmed = false;
        }

        internal static async UniTask MockResendVerificationEmailAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            MockEmailVerifiedAsync(ct).Forget();
        }

        private static async UniTask MockEmailVerifiedAsync(CancellationToken ct)
        {
            await UniTask.Delay(8000, cancellationToken: ct);
            CurrentMockedEmailConfirmed = true;
        }
    }
}
