using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using DCL.Utilities;
using UnityEngine;
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

            int currentLanguage;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE))
                currentLanguage = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE);
            else
            {
                currentLanguage = (int)GetLanguageCodeFromSystem(Application.systemLanguage);
                DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE, currentLanguage, save: true);
            }

            view.DropdownView.Dropdown.SetValueWithoutNotify(currentLanguage);
            view.DropdownView.Dropdown.onValueChanged.AddListener(SetPreferredLanguageSettings);
            view.gameObject.SetActive(this.isTranslationChatEnabled);
        }

        private void SetPreferredLanguageSettings(int index)
        {
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE, index, save: true);
            eventBus.Publish(TranslationSettingsChangeEvent);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetPreferredLanguageSettings);
        }

        private static LanguageCode GetLanguageCodeFromSystem(SystemLanguage systemLang)
        {
            return systemLang switch
                   {
                       SystemLanguage.English => LanguageCode.EN,
                       SystemLanguage.Spanish => LanguageCode.ES,
                       SystemLanguage.French => LanguageCode.FR,
                       SystemLanguage.German => LanguageCode.DE,
                       SystemLanguage.Russian => LanguageCode.RU,
                       SystemLanguage.Portuguese => LanguageCode.PT,
                       SystemLanguage.Italian => LanguageCode.IT,
                       SystemLanguage.Chinese or SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional => LanguageCode.ZH,
                       SystemLanguage.Japanese => LanguageCode.JA,
                       SystemLanguage.Korean => LanguageCode.KO,
                       _ => 0,
                   };
        }
    }
}
