using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Translation.Models;
using DCL.Translation.Service.Models;

namespace DCL.Translation.Service.Provider
{
    public interface IBatchTranslationProvider : ITranslationProvider
    {
        UniTask<TranslationApiResponseBatch> TranslateBatchAsync(string[] texts, LanguageCode target, CancellationToken ct);
    }
}