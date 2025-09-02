using DCL.Translation.Models;

namespace DCL.Translation.Service
{
    public interface ITranslationMemory
    {
        bool TryGet(string messageId, out MessageTranslation translation);
        void Set(string messageId, MessageTranslation translation);
        void UpdateState(string messageId, TranslationState newState, string error = null);
        void SetTranslatedResult(string messageId, TranslationResult result);
    }
}