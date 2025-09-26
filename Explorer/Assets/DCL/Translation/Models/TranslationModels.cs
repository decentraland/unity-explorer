using DCL.Utilities;

namespace DCL.Translation
{
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

    public readonly struct TranslationResultBatch
    {
        public readonly string[] TranslatedText;
        public readonly LanguageCode DetectedSourceLanguage;
        public readonly bool FromCache;

        public TranslationResultBatch(string[] translatedText, LanguageCode detectedSourceLanguage, bool fromCache)
        {
            TranslatedText = translatedText;
            DetectedSourceLanguage = detectedSourceLanguage;
            FromCache = fromCache;
        }
    }
}
