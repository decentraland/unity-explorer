using DCL.Utilities;

namespace DCL.Translation
{
    public class TranslationEvents
    {
        public struct ConversationAutoTranslateToggled
        {
            public string ConversationId;
            public bool IsEnabled;
        }

        public struct MessageTranslationRequested
        {
            public string MessageId;
            public MessageTranslation Translation;
        }

        public struct MessageTranslated
        {
            public string MessageId;
            public MessageTranslation Translation;
        }

        public struct MessageTranslationFailed
        {
            public string MessageId;
            public MessageTranslation Translation;
            public string Error;
        }

        public struct MessageTranslationReverted
        {
            public string MessageId;
            public MessageTranslation Translation;
        }
    }
}
