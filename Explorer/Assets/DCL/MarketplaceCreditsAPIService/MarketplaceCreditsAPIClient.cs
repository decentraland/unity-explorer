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

        public async UniTask<CreditsProgramProgressResponse> GetProgramProgressAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/progress";

            // CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
            //                                                                                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Unity);

            CreditsProgramProgressResponse creditsProgramProgressResponse = await MarketplaceCreditsMockedData.MockCreditsProgramProgressAsync(ct);

            // TODO (Santi): Remove this! This check should be done directly by the progress endpoint
            EmailSubscriptionInfoResponse emailSubscriptionInfoResponse = await GetEmailSubscriptionInfoAsync(ct);
            creditsProgramProgressResponse.user.email = !string.IsNullOrEmpty(emailSubscriptionInfoResponse.unconfirmedEmail) ? emailSubscriptionInfoResponse.unconfirmedEmail : emailSubscriptionInfoResponse.email;
            creditsProgramProgressResponse.user.isEmailConfirmed = string.IsNullOrEmpty(emailSubscriptionInfoResponse.unconfirmedEmail) && !string.IsNullOrEmpty(emailSubscriptionInfoResponse.email);

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
            var formattedCaptchaValue = captchaValue.ToString("F2");
            var jsonBody = $"{{\"x\":{formattedCaptchaValue}}}";

            ClaimCreditsResponse claimCreditsResponseData = await webRequestController.SignedFetchPostAsync(url, jsonBody, ct)
                                                                                      .CreateFromJson<ClaimCreditsResponse>(WRJsonParser.Unity);

            //ClaimCreditsResponse claimCreditsResponseData = await MarketplaceCreditsMockedData.MockClaimCreditsAsync(ct);

            return claimCreditsResponseData;
        }

        public async UniTask SubscribeEmailAsync(string email, CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/set-email";
            var jsonBody = $"{{\"email\":\"{email}\"}}";

            await webRequestController.SignedFetchPutAsync(url, GenericPutArguments.CreateJson(jsonBody), string.Empty, ct)
                                      .WithNoOpAsync();
        }

        // TODO (Santi): Remove this! This check should be done directly by the progress endpoint
        private async UniTask<EmailSubscriptionInfoResponse> GetEmailSubscriptionInfoAsync(CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/subscription";

            EmailSubscriptionInfoResponse emailSubscriptionInfoResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                                                    .CreateFromJson<EmailSubscriptionInfoResponse>(WRJsonParser.Unity);

            return emailSubscriptionInfoResponse;
        }
    }
}
