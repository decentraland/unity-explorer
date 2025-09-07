using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using Utility;

namespace DCL.Settings.ModuleControllers
{
    public class ChatTranslationSettingsController : SettingsFeatureController
    {
        private const string TranslationSettingsChangeEvent = "TranslationSettingsChangeEvent";
        private readonly SettingsDropdownModuleView view;
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly bool isTranslationChatEnabled;
        private readonly IEventBus eventBus;

        public ChatTranslationSettingsController(SettingsDropdownModuleView view,
            ChatSettingsAsset chatSettingsAsset,
            bool isTranslationChatEnabled,
            IEventBus eventBus)
        {
            this.view = view;
            this.chatSettingsAsset = chatSettingsAsset;
            this.isTranslationChatEnabled = isTranslationChatEnabled;
            this.eventBus = eventBus;
            
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE))
            {
                var currentLanguage = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE);
                view.DropdownView.Dropdown.SetValueWithoutNotify(currentLanguage);
            }
            
            view.DropdownView.Dropdown.onValueChanged.AddListener(SetPreferredLanguageSettings);
            view.gameObject.SetActive(this.isTranslationChatEnabled);
        }

        private void SetPreferredLanguageSettings(int index)
        {
            switch (index)
            {
                case (int)ChatPreferredTranslationSettings.DONT_TRANSLATE:
                    chatSettingsAsset.chatPreferredTranslationSettings = ChatPreferredTranslationSettings.DONT_TRANSLATE;
                    break;
                case (int)ChatPreferredTranslationSettings.ES:
                    chatSettingsAsset.chatPreferredTranslationSettings = ChatPreferredTranslationSettings.ES;
                    break;
                case (int)ChatPreferredTranslationSettings.DE:
                    chatSettingsAsset.chatPreferredTranslationSettings = ChatPreferredTranslationSettings.DE;
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatTranslationSettingsController: {index}");
                    return;
            }

            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE, index, save: true);
            eventBus.Publish(TranslationSettingsChangeEvent);
        }
        
        // private static string GetLanguageDisplayName(LanguageCode lang)
        // {
        //     return lang switch
        //     {
        //         LanguageCode.DontTranslate => "Don't Translate",
        //         LanguageCode.EN => "English",
        //         LanguageCode.ES => "Español (Spanish)",
        //         LanguageCode.PT => "Português (Portuguese)",
        //         LanguageCode.DE => "Deutsch (German)",
        //         LanguageCode.ZH => "中文 (Chinese)",
        //         LanguageCode.JA => "日本語 (Japanese)",
        //         LanguageCode.KO => "한국어 (Korean)",
        //         LanguageCode.FR => "Français (French)",
        //         _ => lang.ToString(),
        //     };
        // }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetPreferredLanguageSettings);
        }
    }
}
