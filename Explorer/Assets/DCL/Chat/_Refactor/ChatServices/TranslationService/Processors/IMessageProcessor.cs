using Cysharp.Threading.Tasks;
using DCL.Translation.Models;
using DCL.Utilities;
using System.Threading;

namespace DCL.Chat.ChatServices.TranslationService.Processors
{
    namespace DCL.Translation.Service.Processing
    {
        public interface IMessageProcessor
        {
            UniTask<TranslationResult> ProcessAndTranslateAsync(string rawText, LanguageCode targetLang, CancellationToken ct);
        }
    }
}