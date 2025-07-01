using DCL.Friends.UserBlocking;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Utilities;

namespace DCL.Settings.ModuleControllers
{
    public class HideBlockedUsersChatMessagesController : SettingsFeatureController
    {

        private readonly SettingsToggleModuleView view;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;

        public HideBlockedUsersChatMessagesController(SettingsToggleModuleView view,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy)
        {
            this.view = view;
            this.userBlockingCacheProxy = userBlockingCacheProxy;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_HIDE_BLOCKED_USERS_MESSAGES))
                view.ToggleView.Toggle.isOn = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_HIDE_BLOCKED_USERS_MESSAGES);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetToggle);
            SetToggle(view.ToggleView.Toggle.isOn);
        }

        private void SetToggle(bool enabled)
        {
            if (!userBlockingCacheProxy.Configured) return;

            userBlockingCacheProxy.Object.HideChatMessages = enabled;

            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_HIDE_BLOCKED_USERS_MESSAGES, enabled, save: true);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

    }
}
