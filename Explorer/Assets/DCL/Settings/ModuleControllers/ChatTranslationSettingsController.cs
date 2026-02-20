using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using UnityEngine;
using DCL.Utilities;
using Utility;

namespace DCL.Settings.ModuleControllers
{
    public class ChatTranslationSettingsController : SettingsFeatureController
    {
        private const string TRANSLATION_SETTINGS_CHANGE_EVENT = "TranslationSettingsChangeEvent";
        private readonly SettingsDropdownModuleView view;
        private readonly IEventBus eventBus;

        public ChatTranslationSettingsController(SettingsDropdownModuleView view,
            ChatSettingsAsset chatSettingsAsset,
            IEventBus eventBus)
        {
            this.view = view;
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

            view.DropdownView.Dropdown.template.sizeDelta = new Vector2(view.DropdownView.Dropdown.template.sizeDelta.x, 300f);
            view.DropdownView.Dropdown.onValueChanged.AddListener(SetPreferredLanguageSettings);
            bool isTranslationChatEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.CHAT_TRANSLATION);
            view.gameObject.SetActive(isTranslationChatEnabled);

            if (view.TooltipButtonView != null)
                view.TooltipButtonView.Activate(chatSettingsAsset.CHAT_TRANSLATION_SETTINGS_HOVER_TOOLTIP);
        }

        private void SetPreferredLanguageSettings(int index)
        {
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE, index, save: true);
            eventBus.Publish(TRANSLATION_SETTINGS_CHANGE_EVENT);
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
