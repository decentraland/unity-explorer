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
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_TRANSLATION_PREFERRED_LANGUAGE, index, save: true);
            eventBus.Publish(TranslationSettingsChangeEvent);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetPreferredLanguageSettings);
        }
    }
}
