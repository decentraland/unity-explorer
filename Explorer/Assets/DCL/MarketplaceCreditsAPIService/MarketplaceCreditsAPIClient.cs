using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MarketplaceCreditsAPIService
{
    public class MarketplaceCreditsAPIClient
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private static string mockedEmail = "test@test.com";
        private static bool mockedEmailConfirmed = true;

        private string marketplaceCreditsBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.MarketplaceCredits);

        public MarketplaceCreditsAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<CreditsProgramProgressResponse> GetProgramProgressAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/progress";

            // CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
            //                                                                                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            CreditsProgramProgressResponse creditsProgramProgressResponse = await MockCreditsProgramProgressAsync(mockedEmail, mockedEmailConfirmed, ct);

            return creditsProgramProgressResponse;
        }

        public async UniTask<CreditsProgramProgressResponse> RegisterInTheProgramAsync(string walletId, string email, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/registration/{email}";

            // CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
            //                                                                                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            CreditsProgramProgressResponse programRegistrationResponse = await MockCreditsProgramProgressAsync(email, false, ct);

            return programRegistrationResponse;
        }

        public async UniTask RemoveRegistrationAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/removeRegistration";

            // await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
            //                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            await MockRemoveRegistrationAsync(ct);
        }

        public async UniTask ResendVerificationEmailAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/resendVerificationEmail";

            // await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
            //                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            await MockResendVerificationEmailAsync(ct);
        }

        public async UniTask<Sprite> GenerateCaptchaAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/captcha";

            // Sprite captchaSprite = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
            //                                                  .CreateFromJson<Sprite>(WRJsonParser.Newtonsoft);

            Sprite captchaSprite = await GetSpriteFromUrlAsync("https://i.ibb.co/RG48r508/Test-Captcha.png", ct);

            return captchaSprite;
        }

        public async UniTask<ClaimCreditsResponse> ClaimCreditsAsync(string walletId, float captchaValue, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/claim";

            // ClaimCreditsResponse claimCreditsResponseData = await webRequestController.SignedFetchPostAsync(url, captchaValue.ToString(CultureInfo.InvariantCulture), ct)
            //                                                                           .CreateFromJson<ClaimCreditsResponse>(WRJsonParser.Newtonsoft);

            ClaimCreditsResponse claimCreditsResponseData = await MockClaimCreditsAsync(ct);

            return claimCreditsResponseData;
        }

        private async UniTask<Sprite> GetSpriteFromUrlAsync(string url, CancellationToken ct)
        {
            IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                new GetTextureArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                ct,
                ReportCategory.UI
            );

            var texture = ownedTexture.Texture;
            texture.filterMode = FilterMode.Bilinear;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false);

            return sprite;
        }

        private static async UniTask<CreditsProgramProgressResponse> MockCreditsProgramProgressAsync(string email, bool isEmailConfirmed, CancellationToken ct)
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

            mockedEmail = email;
            mockedEmailConfirmed = isEmailConfirmed;

            return programRegistration;
        }

        private static async UniTask MockRemoveRegistrationAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            mockedEmail = string.Empty;
            mockedEmailConfirmed = false;
        }

        private static async UniTask MockResendVerificationEmailAsync(CancellationToken ct)
        {
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            MockEmailVerifiedAsync(ct).Forget();
        }

        private static async UniTask MockEmailVerifiedAsync(CancellationToken ct)
        {
            await UniTask.Delay(8000, cancellationToken: ct);
            mockedEmailConfirmed = true;
        }

        private static async UniTask<ClaimCreditsResponse> MockClaimCreditsAsync(CancellationToken ct)
        {
            bool randomSuccess = new System.Random().Next(0, 2) == 1;
            float randomClaimedCredits = ((float)new System.Random().NextDouble() * 4) + 1;
            int randomDelay = new System.Random().Next(1000, 3000);
            await UniTask.Delay(randomDelay, cancellationToken: ct);

            ClaimCreditsResponse responseData = new ClaimCreditsResponse
            {
                success = randomSuccess,
                claimedCredits = randomClaimedCredits,
            };

            return responseData;
        }
    }
}
