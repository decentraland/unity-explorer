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

            CreditsProgramProgressResponse creditsProgramProgressResponse = await MarketplaceCreditsMockedData.MockCreditsProgramProgressAsync(MarketplaceCreditsMockedData.CurrentMockedEmail, MarketplaceCreditsMockedData.CurrentMockedEmailConfirmed, ct);

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

        public async UniTask<CreditsProgramProgressResponse> RegisterInTheProgramAsync(string walletId, string email, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/registration/{email}";

            // CreditsProgramProgressResponse creditsProgramProgressResponse = await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
            //                                                                                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            CreditsProgramProgressResponse programRegistrationResponse = await MarketplaceCreditsMockedData.MockCreditsProgramProgressAsync(email, false, ct);

            return programRegistrationResponse;
        }

        public async UniTask RemoveRegistrationAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/removeRegistration";

            // await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
            //                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            await MarketplaceCreditsMockedData.MockRemoveRegistrationAsync(ct);
        }

        public async UniTask ResendVerificationEmailAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/resendVerificationEmail";

            // await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
            //                           .CreateFromJson<CreditsProgramProgressResponse>(WRJsonParser.Newtonsoft);

            await MarketplaceCreditsMockedData.MockResendVerificationEmailAsync(ct);
        }
    }
}
