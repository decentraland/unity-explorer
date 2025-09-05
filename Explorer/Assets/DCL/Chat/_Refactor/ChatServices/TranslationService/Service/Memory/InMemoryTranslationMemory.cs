using System.Collections.Generic;
using DCL.Translation.Models;

namespace DCL.Translation.Service.Memory
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
                translation.State = newState;
                // You could store the error message here if needed
            }
        }

        public void SetTranslatedResult(string messageId, TranslationResult result)
        {
            if (memory.TryGetValue(messageId, out var translation))
            {
                translation.State = TranslationState.Success;
                translation.TranslatedBody = result.TranslatedText;
                translation.DetectedSourceLanguage = result.DetectedSourceLanguage;
            }
        }
    }
}