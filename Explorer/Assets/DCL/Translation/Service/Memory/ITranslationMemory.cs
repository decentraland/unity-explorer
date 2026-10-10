namespace DCL.Translation.Service
{
    public interface ITranslationMemory
    {
        bool TryGet(string messageId, out MessageTranslation translation);
        void Set(string messageId, MessageTranslation translation);
        void SetTranslatedResult(string messageId, TranslationResult result);
        void Clear();
        bool Remove(string messageId);
    }
}