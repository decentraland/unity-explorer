using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

            await UniTask.Delay(TimeSpan.FromMilliseconds(300 + Random.Range(0, 400)), cancellationToken: ct);

            // NOTE: Simulate an occasional failure
            if (requestCount % 5 == 0)
            {
                throw new Exception("Mock Provider: Simulated API failure.");
            }

            // NOTE: Create a fake "translation"
            string translatedText = $"[{target.ToString().ToLower()}] {text}";
            var detectedSource = source == LanguageCode.AutoDetect ? LanguageCode.EN : source;

            return new TranslationResult(translatedText, detectedSource, false);
        }
    }
}