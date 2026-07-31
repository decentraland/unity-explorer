using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using DCL.WebRequests;
using System;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsAPIClient
    {
        private const string NO_DATA_STATE = "NO_DATA";
        private const string SEASON_NOT_STARTED_STATE = "NOT_STARTED";
        public event Action<CreditsProgramProgressResponse> OnProgramProgressUpdated;
        public event Action<UserCreditsResponse> OnUserCreditsFetched;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string marketplaceCreditsBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.MarketplaceCredits);
        private string emailSubscriptionsBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.Notifications);

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

        public virtual async UniTask<UserCreditsResponse> GetUserCreditsAsync(string walletId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/users/{walletId}/credits";

            UserCreditsResponse userCreditsResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                .CreateFromJson<UserCreditsResponse>(WRJsonParser.Unity);

            OnUserCreditsFetched?.Invoke(userCreditsResponse);
            return userCreditsResponse;
        }

        public virtual async UniTask<CreditPacksResponse> GetCreditPacksAsync(CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/credits/packs";

            return await webRequestController.GetAsync(url, ct, ReportCategory.MARKETPLACE_CREDITS)
                                             .CreateFromJson<CreditPacksResponse>(WRJsonParser.Unity);
        }

        public virtual async UniTask<AuthorizeCreditResponse> AuthorizeUsdCreditAsync(int usdPriceCents, string tradeId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/credits/authorize";
            string jsonBody = JsonUtility.ToJson(new AuthorizeUsdCreditBody { usdPriceCents = usdPriceCents, tradeId = tradeId });

            return await webRequestController.SignedFetchPostAsync(url, GenericPostArguments.CreateJson(jsonBody), string.Empty, ct)
                                             .CreateFromJson<AuthorizeCreditResponse>(WRJsonParser.Unity);
        }

        public virtual async UniTask ReleaseUsdIntentsAsync(string[] salts, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/credits/authorize/cancel";
            string jsonBody = JsonUtility.ToJson(new ReleaseUsdIntentsBody { salts = salts });

            await webRequestController.SignedFetchPostAsync(url, GenericPostArguments.CreateJson(jsonBody), string.Empty, ct)
                                      .WithNoOpAsync();
        }

        private async UniTask<SeasonsData> UpdateProgramSeasonsAsync(CancellationToken ct)
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

        public async UniTask<EnumResult<EmailSubscriptionError>> SubscribeEmailAsync(string email, CancellationToken ct)
        {
            var url = $"{emailSubscriptionsBaseUrl}/set-email";
            string jsonBody = JsonUtility.ToJson(new EmailSubscriptionBody
            {
                email = email,
                isCreditsWorkflow = true,
            });

            try
            {
                await webRequestController.SignedFetchPutAsync(url, GenericPostArguments.CreateJson(jsonBody), string.Empty, ct)
                                          .WithNoOpAsync();

                return EnumResult<EmailSubscriptionError>.SuccessResult();
            }
            catch (OperationCanceledException)
            {
                return EnumResult<EmailSubscriptionError>.ErrorResult(EmailSubscriptionError.Cancelled, "Operation was cancelled");
            }
            catch (UnityWebRequestException webRequestException)
            {
                // `email already registered` and `Email domain not allowed` errors are passed under that code
                if (webRequestException.ResponseCode == (long)HttpStatusCode.BadRequest)
                {
                    try
                    {
                        var errorResponse = JsonUtility.FromJson<EmailSubscriptionErrorResponse>(webRequestException.Text);

                        if (!string.IsNullOrEmpty(errorResponse?.message))
                        {
                            return EnumResult<EmailSubscriptionError>.ErrorResult(
                                EmailSubscriptionError.HandledError,
                                errorResponse.message);
                        }
                    }
                    catch (ArgumentException e)
                    {
                        ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"SubscribeEmailAsync - Backend error message failed to be parsed from json, falling back to generic message. \n{e.Message}");
                        // JSON parsing failed, fall through to generic error
                    }
                }

                // All other errors return generic error
                return EnumResult<EmailSubscriptionError>.ErrorResult(EmailSubscriptionError.EmptyError);
            }
            catch (Exception e)
            {
                return EnumResult<EmailSubscriptionError>.ErrorResult(EmailSubscriptionError.EmptyError, e.Message);
            }
        }

        public virtual async UniTask<EnumResult<CheckoutResponse, CreditsCheckoutError>> CreateCheckoutAsync(string packId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/credits/checkout";
            string jsonBody = JsonUtility.ToJson(new CheckoutRequestBody { packId = packId });

            try
            {
                CheckoutResponse checkoutResponse = await webRequestController.SignedFetchPostAsync(url, GenericPostArguments.CreateJson(jsonBody), string.Empty, ct)
                                                                              .CreateFromJson<CheckoutResponse>(WRJsonParser.Unity);

                return EnumResult<CheckoutResponse, CreditsCheckoutError>.SuccessResult(checkoutResponse);
            }
            catch (OperationCanceledException)
            {
                return EnumResult<CheckoutResponse, CreditsCheckoutError>.ErrorResult(CreditsCheckoutError.Cancelled, "Operation was cancelled");
            }
            catch (UnityWebRequestException webRequestException)
            {
                return EnumResult<CheckoutResponse, CreditsCheckoutError>.ErrorResult(
                    MapCheckoutStatusCode(webRequestException.ResponseCode),
                    ParseErrorMessage(webRequestException.Text, nameof(CreateCheckoutAsync)));
            }
            catch (Exception e)
            {
                return EnumResult<CheckoutResponse, CreditsCheckoutError>.ErrorResult(CreditsCheckoutError.NetworkError, e.Message);
            }
        }

        public virtual async UniTask<EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>> GetCheckoutOrderAsync(string orderId, CancellationToken ct)
        {
            var url = $"{marketplaceCreditsBaseUrl}/credits/orders/{orderId}";

            try
            {
                CreditsOrderStatusResponse orderStatusResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                                           .CreateFromJson<CreditsOrderStatusResponse>(WRJsonParser.Unity);

                return EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>.SuccessResult(orderStatusResponse);
            }
            catch (OperationCanceledException)
            {
                return EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>.ErrorResult(CreditsOrderPollError.Cancelled, "Operation was cancelled");
            }
            catch (UnityWebRequestException webRequestException)
            {
                CreditsOrderPollError pollError = webRequestException.ResponseCode == (long)HttpStatusCode.NotFound
                    ? CreditsOrderPollError.NotFound
                    : CreditsOrderPollError.NetworkError;

                return EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>.ErrorResult(
                    pollError,
                    ParseErrorMessage(webRequestException.Text, nameof(GetCheckoutOrderAsync)));
            }
            catch (Exception e)
            {
                return EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>.ErrorResult(CreditsOrderPollError.NetworkError, e.Message);
            }
        }

        internal static CreditsCheckoutError MapCheckoutStatusCode(long responseCode) =>
            responseCode switch
            {
                (long)HttpStatusCode.NotFound => CreditsCheckoutError.FeatureDisabled,
                (long)HttpStatusCode.ServiceUnavailable => CreditsCheckoutError.PaymentsUnavailable,
                (long)HttpStatusCode.BadRequest => CreditsCheckoutError.UnknownPack,
                (long)HttpStatusCode.BadGateway => CreditsCheckoutError.ProviderError,
                _ => CreditsCheckoutError.NetworkError,
            };

        internal static string ParseErrorMessage(string responseText, string caller)
        {
            if (string.IsNullOrEmpty(responseText))
                return string.Empty;

            try
            {
                var errorResponse = JsonUtility.FromJson<CreditsErrorResponse>(responseText);
                return errorResponse?.error ?? string.Empty;
            }
            catch (ArgumentException e)
            {
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{caller} - Backend error message failed to be parsed from json, falling back to an empty message. \n{e.Message}");
                return string.Empty;
            }
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
