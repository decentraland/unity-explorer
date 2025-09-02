using DCL.Translation.Models;

namespace DCL.Translation.Service
{
    /// <summary>
    ///     Role: Prevents redundant API calls, saving time and money.
    ///     Responsibilities: Provides simple TryGet and Set methods.
    ///     The TranslationService will always check this cache
    ///     before calling the ITranslationProvider.
    ///     This can be a composite of a fast in-memory cache and a slower but
    ///     persistent on-disk cache.
    /// </summary>
    public interface ITranslationCache
    {
        bool TryGet(string messageId, LanguageCode targetLang, out TranslationResult result);
        void Set(string messageId, LanguageCode targetLang, TranslationResult result);
    }
}