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

        private static bool isAlreadyRegistered;
        private static int executionNumber = 1;

        private string marketplaceCreditsBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.Badges);

        public MarketplaceCreditsAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<ProgramRegistrationResponse> GetProgramRegistrationInfoAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/registration";

            // ProgramRegistrationResponse programRegistrationResponse = await webRequestController.GetAsync(url, ct, reportData: ReportCategory.MARKETPLACE_CREDITS)
            //                                                                                     .CreateFromJson<ProgramRegistrationResponse>(WRJsonParser.Newtonsoft);

            ProgramRegistrationResponse programRegistrationResponse = await MockProgramRegistrationAsync(ct);

            return programRegistrationResponse;
        }

        public async UniTask<ProgramRegistrationResponse> RegisterInTheProgramAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/registration";

            // ProgramRegistrationResponse programRegistrationResponse = await webRequestController
            //                                                  .SignedFetchPostAsync(url, string.Empty, ct)
            //                                                 .CreateFromJson<ProgramRegistrationResponse>(WRJsonParser.Unity);

            ProgramRegistrationResponse programRegistrationResponse = await MockRegisterInTheProgramAsync(ct);

            return programRegistrationResponse;
        }

        public async UniTask<GoalsOfTheWeekResponse> GetGoalsOfTheWeekAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/goals";

            //GoalsOfTheWeekResponse goalsOfTheWeekResponse = await webRequestController.GetAsync(url, ct, reportData: ReportCategory.MARKETPLACE_CREDITS)
            //                                                                          .CreateFromJson<GoalsOfTheWeekResponse>(WRJsonParser.Newtonsoft);
            GoalsOfTheWeekResponse goalsOfTheWeekResponse = await MockGoalsOfTheWeekAsync(ct);

            return goalsOfTheWeekResponse;
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

        private static async UniTask<ProgramRegistrationResponse> MockProgramRegistrationAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            ProgramRegistrationResponse programRegistration = new ProgramRegistrationResponse
            {
                isRegistered = isAlreadyRegistered,
            };

            return programRegistration;
        }

        private static async UniTask<ProgramRegistrationResponse> MockRegisterInTheProgramAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            ProgramRegistrationResponse programRegistration = new ProgramRegistrationResponse
            {
                isRegistered = true,
            };

            isAlreadyRegistered = true;

            return programRegistration;
        }

        private static async UniTask<GoalsOfTheWeekResponse> MockGoalsOfTheWeekAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            var nonAllClaimedGoalsList = new List<GoalData>
            {
                new ()
                {
                    thumbnail = "https://picsum.photos/100/100",
                    title = "Jump Into Decentraland On 3 Separate Days (Min. 10 min)",
                    progress = new GoalProgressData
                    {
                        totalSteps = 3,
                        stepsDone = 1,
                    },
                    credits = 4,
                    isClaimed = false,
                },
                new ()
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
                new ()
                {
                    thumbnail = "https://picsum.photos/100/100",
                    title = "View 3 New Profiles",
                    progress = new GoalProgressData
                    {
                        totalSteps = 3,
                        stepsDone = 3,
                    },
                    credits = 1,
                    isClaimed = true,
                },
                new ()
                {
                    thumbnail = "https://picsum.photos/100/100",
                    title = "Visit 3 New Parcels",
                    progress = new GoalProgressData
                    {
                        totalSteps = 3,
                        stepsDone = 3,
                    },
                    credits = 1,
                    isClaimed = false,
                },
            };

            var allClaimedGoalsList = new List<GoalData>
            {
                new (isClaimed: true),
                new (isClaimed: true),
                new (isClaimed: true),
                new (isClaimed: true),
            };

            GoalsOfTheWeekResponse goalsOfTheWeekResponse = new GoalsOfTheWeekResponse
            {
                data = new GoalsOfTheWeekData
                {
                    endOfTheWeekDate = "2025-03-16T12:00:00Z",
                    totalCredits = 3.2f,
                    daysToExpire = 15,
                    goals = executionNumber switch
                    {
                        1 => nonAllClaimedGoalsList,
                        2 => allClaimedGoalsList,
                        3 => null,
                    },
                    creditsAvailableToClaim = executionNumber == 1,
                },
            };

            if (executionNumber == 3)
                executionNumber = 1;
            else
                executionNumber++;

            return goalsOfTheWeekResponse;
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
                success = randomSuccess,
            };

            return responseData;
        }
    }
}
