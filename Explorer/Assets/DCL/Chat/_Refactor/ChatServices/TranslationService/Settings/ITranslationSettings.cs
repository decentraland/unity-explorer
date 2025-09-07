using DCL.Translation.Models;

namespace DCL.Translation.Settings
{
    /// <summary>
    ///     Role: A central, read-only source of configuration data.
    ///     Responsibilities: Provides the TranslationService and IConversationTranslationPolicy
    ///     with all the necessary settings (global flag, user's language preference, per-conversation toggles, timeouts).
    /// </summary>
    public interface ITranslationSettings
    {
        bool IsGloballyEnabled { get; }
        LanguageCode PreferredLanguage { get; }
        float TranslationTimeoutSeconds { get; }
        int MaxRetries { get; }
        bool GetAutoTranslateForConversation(string conversationId);
        void SetAutoTranslateForConversation(string conversationId, bool isEnabled);
        bool IsTranslationFeatureActive();
    }
}