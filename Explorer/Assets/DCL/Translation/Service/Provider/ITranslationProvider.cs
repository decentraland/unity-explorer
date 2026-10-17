using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System.Threading;

namespace DCL.Translation.Service
{
    /// <summary>
    ///     Role: The low-level adapter that communicates with an external API or Mock
    ///     Responsibilities: It knows how to format the request, make the network call,
    ///     and parse the response. It has zero knowledge of caching, policies, or the chat application's state.
    ///     It just translates text.
    /// </summary>
    public interface ITranslationProvider
    {
        UniTask<TranslationResult> TranslateAsync(string text, LanguageCode target, CancellationToken ct);
    }
}
