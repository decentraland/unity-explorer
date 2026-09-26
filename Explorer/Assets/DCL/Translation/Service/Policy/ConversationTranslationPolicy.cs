using DCL.Utilities;
using System.Text.RegularExpressions;

namespace DCL.Translation.Service
{
    public class ConversationTranslationPolicy : IConversationTranslationPolicy
    {
        private readonly ITranslationSettings settings;

        private static readonly Regex UrlRegex = new (@"^https?:\/\/\S+$", RegexOptions.Compiled);

        public ConversationTranslationPolicy(ITranslationSettings settings)
        {
            this.settings = settings;
        }

        public bool ShouldAutoTranslate(string message, string conversationId, LanguageCode preferredLanguage)
        {
            // Rule 1: Is the entire feature disabled globally? (Kill Switch)
            if (!settings.IsGloballyEnabled) return false;

            // Rule 3: Has the user disabled auto-translation for this specific conversation?
            if (!settings.GetAutoTranslateForConversation(conversationId)) return false;

            // Rule 4: Is the message trivial or not translatable text?
            if (string.IsNullOrWhiteSpace(message) || UrlRegex.IsMatch(message))
                return false;

            // We will add a language detection rule later, but for now, this is the complete policy.
            // Future Rule 5: if (detectedLanguage == preferredLanguage) return false;

            return true;
        }
    }
}
