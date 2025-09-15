using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Translation.Models;
using DCL.Translation.Service.Models;
using System;
using System.Threading;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using UnityEngine;
using DCL.Translation.Settings;
using CommunicationData.URLHelpers;

namespace DCL.Translation.Service.Provider
{
    public class DclTranslationProvider : ITranslationProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly ITranslationSettings settings;

        private string translateUrl => urlsSource.Url(DecentralandUrl.ChatTranslate);

        public DclTranslationProvider(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource,
            ITranslationSettings settings)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.settings = settings;
        }

        public async UniTask<TranslationResult> TranslateAsync(string text, LanguageCode target, CancellationToken ct)
        {
            string targetCode = target.ToString().ToLower();

            var response = await GetTranslationFromApiAsync(text, "auto", targetCode, ct);

            return new TranslationResult(
                response.translatedText,
                ParseLanguageCode(response.detectedLanguage.language),
                false
            );
        }

        private async UniTask<TranslationApiResponse> GetTranslationFromApiAsync(string text,
            string source,
            string target,
            CancellationToken ct)
        {
            var requestBody = new TranslationRequestBody
            {
                q = text, source = source, target = target, format = "html"
            };

            try
            {
                var commonArgs = new CommonArguments(
                    url: URLAddress.FromString(translateUrl),
                    retryPolicy: RetryPolicy.WithRetries(settings.MaxRetries),
                    timeout: (int)settings.TranslationTimeoutSeconds
                );

                var response = await webRequestController
                    .PostAsync(commonArgs, GenericPostArguments.CreateJson(JsonUtility.ToJson(requestBody)), ct, ReportCategory.CHAT_TRANSLATE)
                    .CreateFromJson<TranslationApiResponse>(WRJsonParser.Newtonsoft);
                return response;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.CHAT_TRANSLATE);
                throw;
            }
        }

        private LanguageCode ParseLanguageCode(string code)
        {
            if (Enum.TryParse<LanguageCode>(code, true, out var languageCode))
                return languageCode;

            return LanguageCode.EN;
        }

        private LanguageCode ParseLanguageCodeSafe(string code, LanguageCode fallback)
        {
            if (Enum.TryParse<LanguageCode>(code, true, out var languageCode))
                return languageCode;

            return fallback;
        }
    }
}
