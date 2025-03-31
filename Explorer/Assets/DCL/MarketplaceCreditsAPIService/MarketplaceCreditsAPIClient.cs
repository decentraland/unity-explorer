using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
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
            //                                                                                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            CreditsProgramProgressResponse creditsProgramProgressResponse = await MarketplaceCreditsMockedData.MockCreditsProgramProgressAsync(ct);

            // TODO (Santi): Remove this! This check should be done directly by the progress endpoint
            EmailSubscriptionInfoResponse emailSubscriptionInfoResponse = await GetEmailSubscriptionInfoAsync(ct);
            creditsProgramProgressResponse.user.email = !string.IsNullOrEmpty(emailSubscriptionInfoResponse.unconfirmedEmail) ? emailSubscriptionInfoResponse.unconfirmedEmail : emailSubscriptionInfoResponse.email;
            creditsProgramProgressResponse.user.isEmailConfirmed = string.IsNullOrEmpty(emailSubscriptionInfoResponse.unconfirmedEmail) && !string.IsNullOrEmpty(emailSubscriptionInfoResponse.email);

            return creditsProgramProgressResponse;
        }

        public async UniTask<Sprite> GenerateCaptchaAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/captcha";

            // CaptchaResponse captchaResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
            //                                                             .CreateFromJson<CaptchaResponse>(WRJsonParser.Newtonsoft);

            CaptchaResponse captchaResponse = await MarketplaceCreditsMockedData.MockCaptchaAsync(ct);

            IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(captchaResponse.captchaUrl)),
                new GetTextureArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                ct,
                ReportCategory.UI
            );

            var texture = ownedTexture.Texture;
            texture.filterMode = FilterMode.Bilinear;

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false);
        }

        public async UniTask<ClaimCreditsResponse> ClaimCreditsAsync(string walletId, float captchaValue, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/claim";

            // ClaimCreditsResponse claimCreditsResponseData = await webRequestController.SignedFetchPostAsync(url, captchaValue.ToString(CultureInfo.InvariantCulture), ct)
            //                                                                           .CreateFromJson<ClaimCreditsResponse>(WRJsonParser.Newtonsoft);

            ClaimCreditsResponse claimCreditsResponseData = await MarketplaceCreditsMockedData.MockClaimCreditsAsync(ct);

            return claimCreditsResponseData;
        }

        public async UniTask SubscribeEmailAsync(string email, CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/set-email";

            await webRequestController.SignedFetchPutAsync(url, GenericPutArguments.CreateJson("{\"email\":\"" + email + "\"}"), string.Empty, ct)
                                      .WithNoOpAsync();
        }

        // TODO (Santi): Remove this! This check should be done directly by the progress endpoint
        private async UniTask<EmailSubscriptionInfoResponse> GetEmailSubscriptionInfoAsync(CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/subscription";

            EmailSubscriptionInfoResponse emailSubscriptionInfoResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                                                    .CreateFromJson<EmailSubscriptionInfoResponse>(WRJsonParser.Newtonsoft);

            return emailSubscriptionInfoResponse;
        }
    }
}
