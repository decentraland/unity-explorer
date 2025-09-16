using DCL.Chat.History;
using DCL.Translation.Models;
using DCL.Utilities;

namespace DCL.Translation.Service.Policy
{
    /// <summary>
    ///     Role: As described above, it makes the initial yes/no decision for auto-translation.
    ///     Responsibilities: Encapsulates all the business rules about when to translate.
    /// </summary>
    public interface IConversationTranslationPolicy
    {
        bool ShouldAutoTranslate(string message, string conversationId, LanguageCode preferredLanguage);
    }
}
