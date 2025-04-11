using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.MarketplaceCreditsAPIService
{
    public class MarketplaceCreditsAPIClient
    {
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

            CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                                                      .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Unity);

            EmailSubscriptionResponse emailSubscriptionResponse = await GetEmailSubscriptionInfoAsync(ct);
            creditsProgramProgressResponse.user.email = !string.IsNullOrEmpty(emailSubscriptionResponse.unconfirmedEmail) ? emailSubscriptionResponse.unconfirmedEmail : emailSubscriptionResponse.email;
            creditsProgramProgressResponse.user.isEmailConfirmed = string.IsNullOrEmpty(emailSubscriptionResponse.unconfirmedEmail) && !string.IsNullOrEmpty(emailSubscriptionResponse.email);

            return creditsProgramProgressResponse;
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
            string jsonBody = JsonUtility.ToJson(new EmailSubscriptionBody { email = email });

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
