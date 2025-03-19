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

        public async UniTask<CreditsProgramProgressResponse> GetProgramProgressAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/progress/{walletId}";

            // CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController.GetAsync(url, ct, reportData: ReportCategory.MARKETPLACE_CREDITS)
            //                                                                                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            CreditsProgramProgressResponse creditsProgramProgressResponse = await MockCreditsProgramProgressAsync(false, ct);

            return creditsProgramProgressResponse;
        }

        public async UniTask<CreditsProgramProgressResponse> RegisterInTheProgramAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/registration";

            // CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController
            //                                                                      .SignedFetchPostAsync(url, string.Empty, ct)
            //                                                                      .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Unity);

            CreditsProgramProgressResponse programRegistrationResponse = await MockCreditsProgramProgressAsync(true, ct);

            return programRegistrationResponse;
        }

        public async UniTask<CaptchaResponse> GenerateCaptchaAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/captcha";

            //CaptchaResponse captchaResponse = await webRequestController.GetAsync(url, ct, reportData: ReportCategory.MARKETPLACE_CREDITS)
            //                                                            .CreateFromJson<CaptchaResponse>(WRJsonParser.Newtonsoft);
            CaptchaResponse captchaResponse = await MockCaptchaAsync(ct);

            return captchaResponse;
        }

        public async UniTask<ClaimCreditsResponse> ClaimCreditsAsync(string walletId, float captchaValue, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/claim";

            // ClaimCreditsResponse claimCreditsResponseData = await webRequestController
            //                                                      .SignedFetchPostAsync(url, captchaValue.ToString(), ct)
            //                                                      .CreateFromJson<ClaimCreditsResponse>(WRJsonParser.Unity);

            ClaimCreditsResponse claimCreditsResponseData = await MockClaimCreditsAsync(ct);

            return claimCreditsResponseData;
        }

        private static async UniTask<CreditsProgramProgressResponse> MockCreditsProgramProgressAsync(bool isRegistered, CancellationToken ct)
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
                    isRegistered = isRegistered,
                    email = "test@test.com",
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

            return programRegistration;
        }

        private static async UniTask<CaptchaResponse> MockCaptchaAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            int randomCaptchaValue = new System.Random().Next(40, 100);
            CaptchaResponse captchaResponse = new CaptchaResponse
            {
                captchaValue = randomCaptchaValue,
            };

            return captchaResponse;
        }

        private static async UniTask<ClaimCreditsResponse> MockClaimCreditsAsync(CancellationToken ct)
        {
            bool randomSuccess = new System.Random().Next(0, 2) == 1;
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            ClaimCreditsResponse responseData = new ClaimCreditsResponse
            {
                success = false,
            };

            return responseData;
        }
    }
}
