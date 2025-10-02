using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Translation.Service
{
    public class DclTranslationProvider : ITranslationProvider, IBatchTranslationProvider
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

        public async UniTask<TranslationApiResponseBatch> TranslateBatchAsync(string[] texts, LanguageCode target, CancellationToken ct)
        {
            if (texts == null || texts.Length == 0)
                return new TranslationApiResponseBatch
                {
                    translatedText = Array.Empty<string>(), detectedLanguage = Array.Empty<DetectedLanguageDto>()
                };

            string targetCode = target.ToString().ToLower();
            var resp = await GetTranslationFromApiBatchAsync(texts, "auto", targetCode, ct);

            // Ignore languages completely, just return translations
            if (resp == null || resp.translatedText == null || resp.translatedText.Length != texts.Length)
                throw new Exception("Batch translation response size mismatch or null translatedTexts.");

            return resp;
        }

        private async UniTask<TranslationApiResponseBatch> GetTranslationFromApiBatchAsync(
            string[] texts, string source, string target, CancellationToken ct)
        {
            var requestBody = new TranslationRequestBodyBatch
            {
                q = texts, source = source, target = target, format = "text"
            };

            ReportHub.Log(ReportCategory.TRANSLATE, TranslationDebug.FormatRequest(translateUrl, "application/json;", requestBody));

            var response =  await webRequestController
                .PostAsync(translateUrl, GenericPostArguments.CreateJson(JsonUtility.ToJson(requestBody)), ct, ReportCategory.TRANSLATE)
                .CreateFromJson<TranslationApiResponseBatch>(WRJsonParser.Newtonsoft);

            ReportHub.Log(ReportCategory.TRANSLATE, TranslationDebug.FormatResponse(translateUrl, response));

            return response;
        }

        private async UniTask<TranslationApiResponse> GetTranslationFromApiAsync(string text,
            string source,
            string target,
            CancellationToken ct)
        {
            var requestBody = new TranslationRequestBody
            {
                q = text, source = source, target = target, format = "text"
            };

            ReportHub.Log(ReportCategory.TRANSLATE, TranslationDebug.FormatRequest(translateUrl, "application/json;", requestBody));

            try
            {
                var commonArgs = new CommonArguments(
                    url: URLAddress.FromString(translateUrl),
                    retryPolicy: RetryPolicy.WithRetries(settings.MaxRetries),
                    timeout: (int)settings.TranslationTimeoutSeconds
                );

                var response = await webRequestController
                    .PostAsync(commonArgs, GenericPostArguments.CreateJson(JsonUtility.ToJson(requestBody)), ct, ReportCategory.TRANSLATE)
                    .CreateFromJson<TranslationApiResponse>(WRJsonParser.Newtonsoft);

                ReportHub.Log(ReportCategory.TRANSLATE, TranslationDebug.FormatResponse(translateUrl, response));

                return response;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.TRANSLATE);
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
