using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System.Threading;

namespace DCL.Translation.Processors
{
    namespace DCL.Translation.Service.Processing
    {
        public interface IMessageProcessor
        {
            UniTask<TranslationResult> ProcessAndTranslateAsync(string rawText, LanguageCode targetLang, CancellationToken ct);
        }
    }
}