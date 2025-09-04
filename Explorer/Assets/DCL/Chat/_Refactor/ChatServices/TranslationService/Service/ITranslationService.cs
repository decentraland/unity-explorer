using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;

namespace DCL.Translation.Service
{
    /// <summary>
    /// Main orchestrator for the translation feature.
    /// It coordinates the policy, cache, memory, and provider to process translation requests
    /// without containing any complex logic itself.
    /// </summary>
    public interface ITranslationService
    {
        void ProcessIncomingMessage(ChatMessage message);
        UniTask TranslateManualAsync(string messageId, CancellationToken ct);
        void RevertToOriginal(string messageId);
    }
}