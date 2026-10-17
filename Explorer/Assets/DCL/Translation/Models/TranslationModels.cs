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
        public string TranslatedBody { get; private set; }
        public TranslationState State { get; private set; }
        public LanguageCode DetectedSourceLanguage { get; private set; }
        public LanguageCode TargetLanguage { get; private set; }

        public MessageTranslation(string originalBody, LanguageCode targetLanguage)
        {
            OriginalBody = originalBody;
            TargetLanguage = targetLanguage;
            State = TranslationState.Original;
        }

        public MessageTranslation(string originalBody, LanguageCode targetLanguage, TranslationState initialState)
        {
            OriginalBody = originalBody;
            TargetLanguage = targetLanguage;
            TranslatedBody = string.Empty;
            State = initialState;
        }

        /// <summary>
        ///     Updates the state of the translation to show it is in progress.
        /// </summary>
        public void SetPending()
        {
            State = TranslationState.Pending;
        }

        /// <summary>
        ///     Updates the state to a final success or failure.
        ///     This is the most common state update from a service.
        /// </summary>
        public void UpdateState(TranslationState newState)
        {
            if (newState == TranslationState.Original) return;

            State = newState;
        }

        /// <summary>
        ///     Sets the final, successful translation result.
        ///     This is the primary method for a successful operation.
        /// </summary>
        public void SetTranslatedResult(string translatedText, LanguageCode detectedSourceLanguage)
        {
            TranslatedBody = translatedText;
            DetectedSourceLanguage = detectedSourceLanguage;
            State = TranslationState.Success;
        }

        /// <summary>
        ///     Reverts the translation back to its original state.
        /// </summary>
        public void RevertToOriginal()
        {
            TranslatedBody = string.Empty;
            State = TranslationState.Original;
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
