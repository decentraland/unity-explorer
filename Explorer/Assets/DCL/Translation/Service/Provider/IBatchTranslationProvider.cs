using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System.Threading;

namespace DCL.Translation.Service
{
    public interface IBatchTranslationProvider : ITranslationProvider
    {
        UniTask<TranslationApiResponseBatch> TranslateBatchAsync(string[] texts, LanguageCode target, CancellationToken ct);
    }
}
