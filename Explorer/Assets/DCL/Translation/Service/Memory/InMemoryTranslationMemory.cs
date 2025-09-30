using System.Collections.Generic;
using DCL.Diagnostics;

namespace DCL.Translation.Service
{
    public class InMemoryTranslationMemory : ITranslationMemory
    {
        private const int MAX_SIZE = 200;
        
        private readonly Dictionary<string, MessageTranslation> memory = new ();
        private readonly Queue<string> insertionOrder = new ();
        
        public bool TryGet(string messageId, out MessageTranslation translation)
        {
            return memory.TryGetValue(messageId, out translation);
        }

        public void Set(string messageId, MessageTranslation translation)
        {
            if (!memory.ContainsKey(messageId))
            {
                // If the memory is already at its maximum size
                if (insertionOrder.Count >= MAX_SIZE)
                {
                    // remove the oldest message ID from the front of the queue.
                    string oldestMessageId = insertionOrder.Dequeue();

                    // And use that ID to remove the corresponding entry from the dictionary.
                    memory.Remove(oldestMessageId);
                    ReportHub.Log(ReportCategory.TRANSLATE, $"Removed oldest translation with ID: {oldestMessageId} to maintain memory size.");
                }

                // Add the new message ID to the end of the queue.
                insertionOrder.Enqueue(messageId);
            }

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
            }
        }

        public void Clear()
        {
            memory.Clear();
            insertionOrder.Clear();
        }
    }
}