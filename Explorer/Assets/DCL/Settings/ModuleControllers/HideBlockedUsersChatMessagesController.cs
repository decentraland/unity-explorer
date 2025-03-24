using DCL.Friends.UserBlocking;
using DCL.Settings.ModuleViews;
using DCL.Utilities;

namespace DCL.Settings.ModuleControllers
{
    public class HideBlockedUsersChatMessagesController : SettingsFeatureController
    {
        private const string HIDE_ENABLED_DATA_STORE_KEY = "Settings_HideBlockedUsersChatMessages";

        private readonly SettingsToggleModuleView view;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;

        public HideBlockedUsersChatMessagesController(SettingsToggleModuleView view,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy)
        {
            this.view = view;
            this.userBlockingCacheProxy = userBlockingCacheProxy;

            if (settingsDataStore.HasKey(HIDE_ENABLED_DATA_STORE_KEY))
                view.ToggleView.Toggle.isOn = settingsDataStore.GetToggleValue(HIDE_ENABLED_DATA_STORE_KEY);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetToggle);
            SetToggle(view.ToggleView.Toggle.isOn);
        }

        private void SetToggle(bool enabled)
        {
            if (!userBlockingCacheProxy.Configured) return;

            userBlockingCacheProxy.Object.HideChatMessages = enabled;

            settingsDataStore.SetToggleValue(HIDE_ENABLED_DATA_STORE_KEY, enabled, save: true);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

    }
}
