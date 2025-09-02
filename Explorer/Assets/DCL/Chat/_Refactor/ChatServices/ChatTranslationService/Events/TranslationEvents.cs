using DCL.Translation.Models;

namespace DCL.Translation.Events
{
    public class TranslationEvents
    {
        public struct PreferredLanguageChanged
        {
            public LanguageCode NewLanguage;
        }

        public struct ConversationAutoTranslateToggled
        {
            public string ConversationId;
            public bool IsEnabled;
        }

        public struct MessageTranslationRequested
        {
            public string MessageId;
        }

        public struct MessageTranslated
        {
            public string MessageId;
        }

        public struct MessageTranslationFailed
        {
            public string MessageId;
            public string Error;
        }

        public struct MessageTranslationReverted
        {
            public string MessageId;
        }
    }
}