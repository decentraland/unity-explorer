using DCL.Utilities;
using System.Collections.Generic;

namespace DCL.Translation.Service
{
    public class InMemoryTranslationCache : ITranslationCache
    {
        private const int MAX_SIZE = 200;
        private readonly Dictionary<string, TranslationResult> cache = new ();
        private readonly Queue<string> insertionOrder = new ();

        private string GetKey(string messageId, LanguageCode targetLang)
        {
            return $"{messageId}:{targetLang}";
        }

        public bool TryGet(string messageId, LanguageCode targetLang, out TranslationResult result)
        {
            return cache.TryGetValue(GetKey(messageId, targetLang), out result);
        }

        public void Set(string messageId, LanguageCode targetLang, TranslationResult result)
        {
            string key = GetKey(messageId, targetLang);

            if (!cache.ContainsKey(key))
            {
                if (insertionOrder.Count >= MAX_SIZE)
                {
                    string oldestKey = insertionOrder.Dequeue();

                    cache.Remove(oldestKey);
                }

                insertionOrder.Enqueue(key);
            }

            cache[key] = result;
        }
    }
}
