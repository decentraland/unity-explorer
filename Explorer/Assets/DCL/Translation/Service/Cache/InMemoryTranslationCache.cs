using DCL.Utilities;
using System.Collections.Generic;

namespace DCL.Translation.Service
{
    public class InMemoryTranslationCache : ITranslationCache
    {
        private readonly Dictionary<string, TranslationResult> cache = new ();

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
            cache[GetKey(messageId, targetLang)] = result;
        }
    }
}
