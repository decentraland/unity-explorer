using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Translation.Models;
using DCL.Translation.Service.Models;
using System;
using System.Threading;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using UnityEngine;

namespace DCL.Translation.Service.Provider
{
    public class DclTranslationProvider : ITranslationProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        private string translateUrl => urlsSource.Url(DecentralandUrl.ChatTranslate);

        public DclTranslationProvider(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
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
                q = text, source = source, target = target
            };

            try
            {
                var response = await webRequestController
                    .PostAsync(translateUrl, GenericPostArguments.CreateJson(JsonUtility.ToJson(requestBody)), ct, ReportCategory.CHAT_TRANSLATE)
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