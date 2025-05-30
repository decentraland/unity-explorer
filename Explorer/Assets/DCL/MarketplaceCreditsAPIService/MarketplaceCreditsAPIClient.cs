using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.MarketplaceCreditsAPIService
{
    public class MarketplaceCreditsAPIClient
    {
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

            await webRequestController.SignedFetchPostAsync(url, GenericUploadArguments.CreateJson(string.Empty), string.Empty, ReportCategory.MARKETPLACE_CREDITS)
                                      .SendAndForgetAsync(ct);
        }

        public async UniTask<CreditsProgramProgressResponse> GetProgramProgressAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/progress";

            CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.MARKETPLACE_CREDITS)
                                                                                                      .CreateFromJsonAsync<CreditsProgramProgressResponse>(WRJsonParser.Unity, ct);

            EmailSubscriptionResponse emailSubscriptionResponse = await GetEmailSubscriptionInfoAsync(ct);
            creditsProgramProgressResponse.user.email = !string.IsNullOrEmpty(emailSubscriptionResponse.unconfirmedEmail) ? emailSubscriptionResponse.unconfirmedEmail : emailSubscriptionResponse.email;
            creditsProgramProgressResponse.user.isEmailConfirmed = string.IsNullOrEmpty(emailSubscriptionResponse.unconfirmedEmail) && !string.IsNullOrEmpty(emailSubscriptionResponse.email);

            OnProgramProgressUpdated?.Invoke(creditsProgramProgressResponse);
            return creditsProgramProgressResponse;
        }

        public async UniTask<Sprite> GenerateCaptchaAsync(CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/captcha";

            IOwnedTexture2D ownedTexture = await webRequestController.SignedFetchTextureAsync(url, new GetTextureArguments(TextureType.Albedo), string.Empty, ReportCategory.MARKETPLACE_CREDITS)
                                                                     .CreateTextureAsync(TextureWrapMode.Clamp, ct: ct);

            Texture2D texture = ownedTexture.Texture;

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false);
        }

        public async UniTask<ClaimCreditsResponse> ClaimCreditsAsync(float captchaValue, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/captcha";
            string jsonBody = JsonUtility.ToJson(new ClaimCreditsBody { x = captchaValue });

            ClaimCreditsResponse claimCreditsResponseData = await webRequestController.SignedFetchPostAsync(url, GenericUploadArguments.CreateJson(jsonBody), string.Empty, ReportCategory.MARKETPLACE_CREDITS)
                                                                                      .CreateFromJsonAsync<ClaimCreditsResponse>(WRJsonParser.Unity, ct);

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

            await webRequestController.SignedFetchPutAsync(url, GenericUploadArguments.CreateJson(jsonBody), string.Empty, ReportCategory.MARKETPLACE_CREDITS)
                                      .SendAndForgetAsync(ct);
        }

        private async UniTask<EmailSubscriptionResponse> GetEmailSubscriptionInfoAsync(CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/subscription";

            EmailSubscriptionResponse emailSubscriptionResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.MARKETPLACE_CREDITS)
                                                                                            .CreateFromJsonAsync<EmailSubscriptionResponse>(WRJsonParser.Unity, ct);

            return emailSubscriptionResponse;
        }
    }
}
