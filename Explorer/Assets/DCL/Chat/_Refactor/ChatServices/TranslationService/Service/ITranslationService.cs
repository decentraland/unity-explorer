using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Translation.Service
{
    /// <summary>
    /// Main orchestrator for the translation feature.
    /// It coordinates the policy, cache, memory, and provider to process translation requests
    /// without containing any complex logic itself.
    /// </summary>
    public interface ITranslationService
    {
        void ProcessIncomingMessage(string messageId, string originalText, string conversationId);
        UniTask TranslateManualAsync(string messageId, string originalText, CancellationToken ct);
        void RevertToOriginal(string messageId);
    }
}
