using DCL.Friends.UserBlocking;
using DCL.Prefs;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class HideBlockedUsersChatMessagesController : SettingsFeatureController
    {

        private readonly SettingsToggleModuleView view;
        private readonly IUserBlockingCache userBlockingCache;

        public HideBlockedUsersChatMessagesController(SettingsToggleModuleView view,
            IUserBlockingCache userBlockingCache)
        {
            this.view = view;
            this.userBlockingCache = userBlockingCache;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_HIDE_BLOCKED_USERS_MESSAGES))
                view.ToggleView.Toggle.isOn = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_HIDE_BLOCKED_USERS_MESSAGES);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetToggle);
            SetToggle(view.ToggleView.Toggle.isOn);
        }

        private void SetToggle(bool enabled)
        {
            userBlockingCache.HideChatMessages = enabled;

            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_HIDE_BLOCKED_USERS_MESSAGES, enabled, save: true);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

    }
}
