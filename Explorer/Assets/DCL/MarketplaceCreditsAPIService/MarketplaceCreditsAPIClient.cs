using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.MarketplaceCreditsAPIService
{
    public class MarketplaceCreditsAPIClient
    {
        private const string NO_DATA_STATE = "NO_DATA";
        private const string SEASON_NOT_STARTED_STATE = "NOT_STARTED";
        public event Action<CreditsProgramProgressResponse> OnProgramProgressUpdated;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string marketplaceCreditsBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.MarketplaceCredits);
        private string emailSubscriptionsBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.EmailSubscriptions);

        public MarketplaceCreditsAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask MarkUserAsStartedProgramAsync(CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users";

            await webRequestController.SignedFetchPostAsync(url, GenericPostArguments.CreateJson(string.Empty), string.Empty, ct)
                                      .WithNoOpAsync();
        }

        public async UniTask<CreditsProgramProgressResponse> GetProgramProgressAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/progress";

            CreditsProgramProgressResponse creditsProgramProgressResponse = 
                await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                    .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Unity);

            EmailSubscriptionResponse emailSubscriptionResponse = await GetEmailSubscriptionInfoAsync(ct);
            SeasonsData seasonResult = await UpdateProgramSeasonsAsync(ct);

            creditsProgramProgressResponse.lastSeason = seasonResult!.lastSeason;
            creditsProgramProgressResponse.currentSeason = seasonResult!.currentSeason.season;
            // Setting this here, so we don't need to check for null everytime.
            if (seasonResult!.currentSeason.season.state == null)
                creditsProgramProgressResponse.currentSeason.state = NO_DATA_STATE;
            creditsProgramProgressResponse.currentWeek = seasonResult!.currentSeason.week;
            creditsProgramProgressResponse.nextSeason = seasonResult!.nextSeason;
            creditsProgramProgressResponse.user.email = 
                !string.IsNullOrEmpty(emailSubscriptionResponse.unconfirmedEmail) 
                    ? emailSubscriptionResponse.unconfirmedEmail 
                    : emailSubscriptionResponse.email;
            creditsProgramProgressResponse.user.isEmailConfirmed = 
                string.IsNullOrEmpty(emailSubscriptionResponse.unconfirmedEmail) 
                && !string.IsNullOrEmpty(emailSubscriptionResponse.email);

            OnProgramProgressUpdated?.Invoke(creditsProgramProgressResponse);
            return creditsProgramProgressResponse;
        }

        private async Task<SeasonsData> UpdateProgramSeasonsAsync(CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/seasons";
            
            var result = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                    .CreateFromJson<SeasonsData>(WRJsonParser.Unity);

            result.lastSeason.state ??= NO_DATA_STATE;
            result.currentSeason.season.state ??= NO_DATA_STATE;
            result.nextSeason.state = string.IsNullOrEmpty(result.nextSeason.startDate) ? NO_DATA_STATE : SEASON_NOT_STARTED_STATE;

            return result;
        }

        public async UniTask<Sprite> GenerateCaptchaAsync(CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/captcha";

            DownloadHandler downloadHandler = null;

            try
            {
                downloadHandler = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                            .ExposeDownloadHandlerAsync();

                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(downloadHandler.data);
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false);
            }
            finally
            {
                downloadHandler?.Dispose();
            }
        }

        public async UniTask<ClaimCreditsResponse> ClaimCreditsAsync(float captchaValue, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/captcha";
            string jsonBody = JsonUtility.ToJson(new ClaimCreditsBody { x = captchaValue });

            ClaimCreditsResponse claimCreditsResponseData = await webRequestController.SignedFetchPostAsync(url, GenericPostArguments.CreateJson(jsonBody), string.Empty, ct)
                                                                                      .CreateFromJson<ClaimCreditsResponse>(WRJsonParser.Unity);

            return claimCreditsResponseData;
        }

        public async UniTask SubscribeEmailAsync(string email, CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/set-email";
            string jsonBody = JsonUtility.ToJson(new EmailSubscriptionBody
            {
                email = email,
                isCreditsWorkflow = true,
            });

            await webRequestController.SignedFetchPutAsync(url, GenericPutArguments.CreateJson(jsonBody), string.Empty, ct)
                                      .WithNoOpAsync();
        }

        private async UniTask<EmailSubscriptionResponse> GetEmailSubscriptionInfoAsync(CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/subscription";

            EmailSubscriptionResponse emailSubscriptionResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                                            .CreateFromJson<EmailSubscriptionResponse>(WRJsonParser.Unity);

            return emailSubscriptionResponse;
        }
    }
}
