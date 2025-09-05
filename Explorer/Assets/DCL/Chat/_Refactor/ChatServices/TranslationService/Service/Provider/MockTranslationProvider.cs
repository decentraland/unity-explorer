using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices.TranslationService.Helpers;
using DCL.Translation.Models;
using Random = UnityEngine.Random;

namespace DCL.Translation.Service.Provider
{
    public class MockTranslationProvider : ITranslationProvider
    {
        private int requestCount  ;

        public UniTask<LanguageCode> DetectLanguageAsync(string text, CancellationToken ct)
        {
            return UniTask.FromResult(LanguageCode.EN);
        }

        public async UniTask<TranslationResult> TranslateAsync(string text, LanguageCode source, LanguageCode target, CancellationToken ct)
        {
            requestCount++;

            await UniTask.Delay(TimeSpan.FromMilliseconds(1500 + Random.Range(0, 1500)), cancellationToken: ct);

            if (requestCount % 5 == 0)
                throw new Exception("Mock Provider: Simulated API failure.");

            string translatedText = GenerateMockTranslationText(text, target);
            var detectedSource = source == LanguageCode.AutoDetect ? LanguageCode.EN : source;

            return new TranslationResult(translatedText, detectedSource, false);
        }

        /// <summary>
        ///     Generates a realistic, variable-length "Lorem Ipsum" string to simulate a translation.
        /// </summary>
        /// <param name="originalText">The original text, used to influence the length of the generated text.</param>
        /// <param name="targetLanguage">The target language, used to prepend a debug tag.</param>
        /// <returns>A mock translated string.</returns>
        private static string GenerateMockTranslationText(string originalText, LanguageCode targetLanguage)
        {
            // Base the length of the mock translation on the original text's length for more variety.
            // This helps test UI resizing with different content lengths.
            int minWords = 5;
            int maxWords = 15 + originalText.Length / 5;
            string loremIpsumText = LoremIpsumGenerator.Generate(minWords, maxWords);

            // Prepend the language code for easy identification during testing.
            return $"[{targetLanguage.ToString().ToLower()}] {loremIpsumText}";
        }
    }
}