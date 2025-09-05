using DCL.Chat.ChatConfig;
using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.Translation.Models;

namespace DCL.Translation.Settings
{
    public class PlayerPrefsTranslationSettings : ITranslationSettings
    {
        private readonly ChatConfig chatConfig;
        
        private const string AUTO_TRANSLATE_PREFIX = "chat.translation.auto.";

        public PlayerPrefsTranslationSettings(ChatConfig chatConfig)
        {
            this.chatConfig = chatConfig;
        }

        public int MaxRetries => chatConfig.TranslationMaxRetries;
        public float TranslationTimeoutSeconds => chatConfig.TranslationTimeoutSeconds;
        
        public bool IsGloballyEnabled => 
            chatConfig.ForceEnableTranslations ||
            FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_TRANSLATION_ENABLED);
        
        public LanguageCode PreferredLanguage
        {
            get
            {
                if (chatConfig.ForceEnableTranslations)
                    return chatConfig.DefaultLanguage;

                return (LanguageCode)DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE, (int)LanguageCode.DontTranslate);
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