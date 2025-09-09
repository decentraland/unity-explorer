namespace DCL.Translation.Models
{
    public enum LanguageCode
    {
        DontTranslate = 0,
        EN = 1,
        ES = 2,
        FR = 3,
        DE = 4,
        PT = 5,
        IT = 6,
        ZH = 7,
        JA = 8
    }

    public enum TranslationState
    {
        Original,
        Pending,
        Success,
        Failed
    }

    public class MessageTranslation
    {
        public string OriginalBody { get; }
        public string TranslatedBody { get; set; }
        public TranslationState State { get; set; }
        public LanguageCode DetectedSourceLanguage { get; set; }
        public LanguageCode TargetLanguage { get; set; }

        public MessageTranslation(string originalBody, LanguageCode targetLanguage)
        {
            OriginalBody = originalBody;
            TargetLanguage = targetLanguage;
            State = TranslationState.Original; // Default state
        }
    }

    public readonly struct TranslationResult
    {
        public readonly string TranslatedText;
        public readonly LanguageCode DetectedSourceLanguage;
        public readonly bool FromCache;

        public TranslationResult(string translatedText, LanguageCode detectedSourceLanguage, bool fromCache)
        {
            TranslatedText = translatedText;
            DetectedSourceLanguage = detectedSourceLanguage;
            FromCache = fromCache;
        }
    }
}