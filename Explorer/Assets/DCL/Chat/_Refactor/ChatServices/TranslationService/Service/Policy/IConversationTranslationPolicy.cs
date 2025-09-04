using DCL.Chat.History;
using DCL.Translation.Models;

namespace DCL.Translation.Service.Policy
{
    /// <summary>
    ///     Role: As described above, it makes the initial yes/no decision for auto-translation.
    ///     Responsibilities: Encapsulates all the business rules about when to translate.
    /// </summary>
    public interface IConversationTranslationPolicy
    {
        bool ShouldAutoTranslate(ChatMessage message, string conversationId, LanguageCode preferredLanguage);
    }
}