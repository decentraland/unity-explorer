using DCL.Prefs;
using DCL.Translation.Models;

namespace DCL.Translation.Service
{
    public class PlayerPrefsTranslationSettings : ITranslationSettings
    {
        private const string AUTO_TRANSLATE_PREFIX = "chat.translation.auto.";
        private const string PREFERRED_LANGUAGE_KEY = "user.translation.preferredLanguage";

        public bool IsGloballyEnabled { get; } = true;
        public float TranslationTimeoutSeconds => 3.0f; // Could also come from a config
        public int MaxRetries => 1;

        public LanguageCode PreferredLanguage
        {
            get => (LanguageCode)DCLPlayerPrefs.GetInt(PREFERRED_LANGUAGE_KEY, (int)LanguageCode.DontTranslate);
            set => DCLPlayerPrefs.SetInt(PREFERRED_LANGUAGE_KEY, (int)value);
        }

        public bool GetAutoTranslateForConversation(string conversationId)
        {
            return DCLPlayerPrefs.GetBool($"{AUTO_TRANSLATE_PREFIX}{conversationId}");
        }

        public void SetAutoTranslateForConversation(string conversationId, bool isEnabled)
        {
            DCLPlayerPrefs.SetBool($"{AUTO_TRANSLATE_PREFIX}{conversationId}", isEnabled);
            DCLPlayerPrefs.Save();
        }
    }
}