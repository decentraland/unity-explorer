using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;

namespace DCL.Translation.Service
{
    public interface ITranslationService
    {
        void ProcessIncomingMessage(string channelId, ChatMessage message);
        UniTask TranslateManualAsync(string messageId, CancellationToken ct);
        void RevertToOriginal(string messageId);
    }
}