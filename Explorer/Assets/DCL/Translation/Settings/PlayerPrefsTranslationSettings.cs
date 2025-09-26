using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.Utilities;

namespace DCL.Translation
{
    public class PlayerPrefsTranslationSettings : ITranslationSettings
    {
        public bool ForceEnableTranslations = false;
        private const string AUTO_TRANSLATE_PREFIX = "chat.translation.auto.";

        public int MaxRetries => 1;
        public float TranslationTimeoutSeconds => 10;

        /// <summary>
        ///     Is feature remotely enabled through feature flag or forced through chat config.
        /// </summary>
        public bool IsGloballyEnabled =>
            ForceEnableTranslations ||
            FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_TRANSLATION_ENABLED);

        /// <summary>
        ///     Is feature active meaning that translations is activated and can be used.
        /// </summary>
        public bool IsTranslationFeatureActive()
        {
            return IsGloballyEnabled;
        }

        public LanguageCode PreferredLanguage
        {
            get
            {
                if (ForceEnableTranslations)
                    return LanguageCode.ES;

                return (LanguageCode)DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE);
            }
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
