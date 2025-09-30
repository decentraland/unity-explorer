using System.Collections.Generic;

namespace DCL.Translation.Service
{
    public class InMemoryTranslationMemory : ITranslationMemory
    {
        private readonly Dictionary<string, MessageTranslation> memory = new ();

        public bool TryGet(string messageId, out MessageTranslation translation)
        {
            return memory.TryGetValue(messageId, out translation);
        }

        public void Set(string messageId, MessageTranslation translation)
        {
            memory[messageId] = translation;
        }

        public void UpdateState(string messageId, TranslationState newState, string error = null)
        {
            if (memory.TryGetValue(messageId, out var translation))
            {
                translation.UpdateState(newState);
            }
        }

        public void SetTranslatedResult(string messageId, TranslationResult result)
        {
            if (memory.TryGetValue(messageId, out var translation))
            {
                translation.SetTranslatedResult(result.TranslatedText, result.DetectedSourceLanguage);
                translation.UpdateState(TranslationState.Success);
            }
        }
    }
}